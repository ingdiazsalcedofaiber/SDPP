using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

/// <summary>Backs POST /access/{token}/consent — records explicit acceptance of the electronic-
/// signature consent declaration. Must be called before CompleteRecipientSigningCommand will allow
/// this recipient to sign; SignatureEnvelope.RegisterSignature enforces this fail-closed in the
/// domain, this handler just records the acceptance itself.</summary>
public sealed record RegisterConsentCommand(string RawToken, string? SessionToken) : ICommand;

public sealed class RegisterConsentHandler(
    ISignerAccessChallengeRepository challengeRepository,
    ISignatureEnvelopeRepository envelopeRepository,
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor)
    : IRequestHandler<RegisterConsentCommand, Result>
{
    public async Task<Result> Handle(RegisterConsentCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawToken)));
        var challenge = await challengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null || !challenge.IsLinkUsable)
        {
            return Result.Failure("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var envelope = await envelopeRepository.GetByRecipientIdAsync(challenge.RecipientId, cancellationToken);
        var recipient = envelope?.Recipients.FirstOrDefault(r => r.Id == challenge.RecipientId);
        if (envelope is null || recipient is null)
        {
            return Result.Failure("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }
        if (!RecipientAccessAuthorization.CanAct(recipient, challenge, currentActor, request.SessionToken))
        {
            return Result.Failure("No estás autenticado para actuar como este destinatario.", "FORBIDDEN");
        }

        try
        {
            var authMethod = recipient.MatchedUserId is not null ? "SdppSession" : "EmailOtp";
            envelope.RegisterConsent(recipient.Id, currentActor.IpAddress, currentActor.UserAgent, authMethod);
        }
        catch (SDPP.BuildingBlocks.Domain.DomainException ex)
        {
            return Result.Failure(ex.Message, "INVALID_STATE");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
