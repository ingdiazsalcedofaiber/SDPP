using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

/// <summary>PLAINTEXT CODE IS TEMPORARY — Phase 1 has no email infrastructure yet (see the module
/// redesign plan's Phase 3), so there is currently no other way to deliver the OTP to an external
/// recipient than returning it here for the frontend/tester to use directly. The code itself is
/// real (cryptographically random, hashed at rest, time/attempt-limited) — only the delivery
/// channel is deferred. Once Phase 3 wires a real IEmailSender, this field must be removed from the
/// response; leaving it would let anyone who can see the HTTP response bypass the OTP's whole
/// purpose.</summary>
public sealed record RequestOtpResult(string Code);

public sealed record RequestOtpCommand(string RawToken) : ICommand<RequestOtpResult>;

public sealed class RequestOtpHandler(
    ISignerAccessChallengeRepository challengeRepository, ISignatureEnvelopeRepository envelopeRepository, IUnitOfWork unitOfWork,
    ICurrentActor currentActor, IIntegrationEventPublisher integrationEventPublisher)
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

        return Result.Success(new RequestOtpResult(code));
    }
}
