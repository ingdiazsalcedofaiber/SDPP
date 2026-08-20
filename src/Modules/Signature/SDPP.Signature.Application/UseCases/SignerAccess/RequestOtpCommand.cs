using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

/// <summary>Empty on purpose — the code itself must never appear in the HTTP response (see
/// RequestOtpHandler: it's emailed, not returned). An earlier version of this returned the code
/// directly because no real email delivery existed yet; that made the OTP factor worthless (anyone
/// who could see the response — not just the real recipient's inbox — could read the code), so once
/// IEmailSender had a real SMTP implementation (see SmtpEmailSender) this was fixed to actually use
/// it instead.</summary>
public sealed record RequestOtpResult;

public sealed record RequestOtpCommand(string RawToken) : ICommand<RequestOtpResult>;

public sealed class RequestOtpHandler(
    ISignerAccessChallengeRepository challengeRepository, ISignatureEnvelopeRepository envelopeRepository, IUnitOfWork unitOfWork,
    ICurrentActor currentActor, IIntegrationEventPublisher integrationEventPublisher, IEmailSender emailSender)
    : IRequestHandler<RequestOtpCommand, Result<RequestOtpResult>>
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);

    public async Task<Result<RequestOtpResult>> Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawToken)));
        var challenge = await challengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null || !challenge.IsLinkUsable)
        {
            return Result.Failure<RequestOtpResult>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var envelope = await envelopeRepository.GetByRecipientIdAsync(challenge.RecipientId, cancellationToken);
        var recipient = envelope?.Recipients.FirstOrDefault(r => r.Id == challenge.RecipientId);
        if (recipient is null)
        {
            return Result.Failure<RequestOtpResult>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }
        if (recipient.MatchedUserId is not null)
        {
            return Result.Failure<RequestOtpResult>(
                "Este firmante tiene una cuenta SDPP y debe iniciar sesión en la plataforma en vez de usar un código.", "OTP_NOT_APPLICABLE");
        }
        if (recipient.InPerson)
        {
            return Result.Failure<RequestOtpResult>(
                "Este firmante firma de forma presencial y no necesita código.", "OTP_NOT_APPLICABLE");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
        challenge.IssueOtp(codeHash, OtpLifetime);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new RecipientOtpRequestedV1(
            Guid.NewGuid(), DateTime.UtcNow, envelope!.Id, recipient.Id, recipient.Email, currentActor.IpAddress, currentActor.UserAgent),
            cancellationToken);

        // The code is delivered ONLY here — never in the API response (see RequestOtpResult's doc
        // comment on why that matters). Best-effort: a delivery failure shouldn't roll back an
        // already-issued, already-hashed-and-stored challenge, and SmtpEmailSender/LoggingEmailSender
        // each log their own outcome (see CompleteRecipientSigningCommand's identical reasoning for
        // the certificate email).
        try
        {
            await emailSender.SendAsync(
                recipient.Email, "Código de verificación — SDPP",
                $"<p>Tu código de verificación para firmar \"<strong>{envelope!.Title}</strong>\" es:</p><p style=\"font-size:24px;font-weight:700;letter-spacing:4px;\">{code}</p><p>Vence en 10 minutos.</p>",
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Best-effort by design — see the comment above.
        }

        return Result.Success(new RequestOtpResult());
    }
}
