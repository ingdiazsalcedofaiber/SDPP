using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

public sealed record VerifyOtpResult(string SessionToken);

public sealed record VerifyOtpCommand(string RawToken, string Code) : ICommand<VerifyOtpResult>;

/// <summary>Validates the OTP and, on success, issues the bearer session token that every
/// subsequent field-fill/complete/decline call must present — see SignerAccessChallenge's
/// three-sub-state comment for why the link token alone isn't enough.</summary>
public sealed class VerifyOtpHandler(
    ISignerAccessChallengeRepository challengeRepository, ISignatureEnvelopeRepository envelopeRepository, IUnitOfWork unitOfWork,
    ICurrentActor currentActor, IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<VerifyOtpCommand, Result<VerifyOtpResult>>
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(2);

    public async Task<Result<VerifyOtpResult>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawToken)));
        var challenge = await challengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null || !challenge.IsLinkUsable)
        {
            return Result.Failure<VerifyOtpResult>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }
        if (!challenge.IsOtpUsable)
        {
            return Result.Failure<VerifyOtpResult>("El código expiró o no fue solicitado. Solicita uno nuevo.", "OTP_INVALID");
        }

        var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Code)));
        if (challenge.OtpCodeHash != codeHash)
        {
            challenge.RegisterOtpFailedAttempt();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<VerifyOtpResult>("El código ingresado no es válido.", "INVALID_CODE");
        }

        challenge.ConsumeOtp();
        var sessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sessionTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)));
        challenge.IssueSession(sessionTokenHash, SessionLifetime);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var envelope = await envelopeRepository.GetByRecipientIdAsync(challenge.RecipientId, cancellationToken);
        var recipient = envelope?.Recipients.FirstOrDefault(r => r.Id == challenge.RecipientId);
        if (envelope is not null && recipient is not null)
        {
            await integrationEventPublisher.PublishAsync(new RecipientOtpValidatedV1(
                Guid.NewGuid(), DateTime.UtcNow, envelope.Id, recipient.Id, recipient.Email, currentActor.IpAddress, currentActor.UserAgent),
                cancellationToken);
        }

        return Result.Success(new VerifyOtpResult(sessionToken));
    }
}
