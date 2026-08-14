using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.ResendRecipientAccess;

/// <summary>AccessToken is the raw, unhashed link token — see SendEnvelopeResult's doc comment for
/// why this is a real, permanent capability (not just a test aid): Phase 1 has no email delivery
/// yet, so this is the creator's (or the matched recipient's own) only way to (re)obtain a
/// recipient's link manually; once Phase 3 wires real email, this doubles as the "reenviar
/// recordatorio" action from the spec.</summary>
public sealed record ResendRecipientAccessResult(string AccessToken);

public sealed record ResendRecipientAccessCommand(Guid EnvelopeId, Guid RecipientId) : ICommand<ResendRecipientAccessResult>;

public sealed class ResendRecipientAccessHandler(
    ISignatureEnvelopeRepository envelopeRepository, ISignerAccessChallengeRepository challengeRepository,
    IUnitOfWork unitOfWork, ICurrentActor currentActor, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<ResendRecipientAccessCommand, Result<ResendRecipientAccessResult>>
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(30);

    public async Task<Result<ResendRecipientAccessResult>> Handle(ResendRecipientAccessCommand request, CancellationToken cancellationToken)
    {
        var envelope = await envelopeRepository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<ResendRecipientAccessResult>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }

        var recipient = envelope.Recipients.FirstOrDefault(r => r.Id == request.RecipientId);

        // Either the creator sharing/resending on someone else's behalf, or an internal recipient
        // fetching their own signing link straight from their inbox (self-service — no reason to
        // force every internal user through the creator for something that's rightfully theirs).
        var isSelfService = recipient?.MatchedUserId == currentActor.UserId;
        if (!UseCases.EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()) && !isSelfService)
        {
            return Result.Failure<ResendRecipientAccessResult>("No tienes permiso para gestionar este sobre.", "FORBIDDEN");
        }

        if (recipient is null)
        {
            return Result.Failure<ResendRecipientAccessResult>("El destinatario no pertenece a este sobre.", "RECIPIENT_NOT_FOUND");
        }
        if (recipient.Status is RecipientStatus.Pending or RecipientStatus.Signed or RecipientStatus.Declined or RecipientStatus.Expired)
        {
            return Result.Failure<ResendRecipientAccessResult>(
                $"No se puede reenviar el acceso de un destinatario en estado '{recipient.Status}'.", "INVALID_STATE");
        }

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        challengeRepository.Add(SignerAccessChallenge.Issue(recipient.Id, tokenHash, LinkLifetime));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new ResendRecipientAccessResult(rawToken));
    }
}
