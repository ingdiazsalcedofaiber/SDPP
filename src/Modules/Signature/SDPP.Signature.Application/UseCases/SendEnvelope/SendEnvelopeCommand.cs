using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Application.UseCases.SendEnvelope;

public sealed record DispatchedRecipient(Guid RecipientId, string Email, string AccessToken);

/// <summary>AccessTokens are the RAW, unhashed link tokens for every recipient in the first wave —
/// returned to the creator (never persisted in plaintext, see SignerAccessChallenge.TokenHash) so
/// the frontend can offer "copy invitation link" as a real, working fallback for Phase 1/2, before
/// Phase 3 wires up actually emailing them.</summary>
public sealed record SendEnvelopeResult(Guid EnvelopeId, IReadOnlyList<DispatchedRecipient> Dispatched);

public sealed record SendEnvelopeCommand(Guid EnvelopeId) : ICommand<SendEnvelopeResult>;

/// <summary>Resolves each recipient's SDPP account (if any) via Identity, transitions the envelope
/// to Sent/Pending, and issues a SignerAccessChallenge (link token, no OTP yet — that's requested
/// separately, on-demand, once the recipient opens the link) for the first dispatch wave. See
/// SignatureEnvelope.Send for the sequential-vs-parallel wave logic.</summary>
public sealed class SendEnvelopeHandler(
    ISignatureEnvelopeRepository repository,
    ISignerAccessChallengeRepository challengeRepository,
    IIdentityClient identityClient,
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher,
    IOrganizationContextProvider organizationContextProvider,
    INotificationRepository notificationRepository)
    : IRequestHandler<SendEnvelopeCommand, Result<SendEnvelopeResult>>
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(30);

    public async Task<Result<SendEnvelopeResult>> Handle(SendEnvelopeCommand request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<SendEnvelopeResult>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }
        if (!EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()))
        {
            return Result.Failure<SendEnvelopeResult>("No tienes permiso para enviar este sobre.", "FORBIDDEN");
        }

        foreach (var recipient in envelope.Recipients)
        {
            var match = await identityClient.LookupByEmailAsync(recipient.Email, cancellationToken);
            envelope.ResolveRecipientMatch(recipient.Id, match?.UserId);
        }

        IReadOnlyList<EnvelopeRecipient> firstWave;
        try
        {
            firstWave = envelope.Send();
        }
        catch (SDPP.BuildingBlocks.Domain.DomainException ex)
        {
            return Result.Failure<SendEnvelopeResult>(ex.Message, "INVALID_STATE");
        }

        var dispatched = new List<DispatchedRecipient>();
        foreach (var recipient in firstWave)
        {
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            challengeRepository.Add(SignerAccessChallenge.Issue(recipient.Id, tokenHash, LinkLifetime));
            dispatched.Add(new DispatchedRecipient(recipient.Id, recipient.Email, rawToken));

            if (recipient.MatchedUserId is { } matchedUserId)
            {
                notificationRepository.Add(Domain.Aggregates.InAppNotification.Create(
                    matchedUserId, Domain.Enums.NotificationType.EnvelopeSent,
                    "Documento para firmar", $"{currentActor.FullName} te envió \"{envelope.Title}\" para firmar.", envelope.Id));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new SignatureEnvelopeSentV1(
            Guid.NewGuid(), DateTime.UtcNow, envelope.Id, envelope.SourceDocumentId, envelope.Title,
            envelope.SigningMode.ToString(), envelope.Recipients.Count, envelope.DueDateUtc,
            new ActorSnapshot(
                currentActor.UserId, currentActor.FullName, currentActor.Email, currentActor.Domain,
                currentActor.IpAddress, Hostname: null, OperatingSystem: null, currentActor.UserAgent, MacAddress: null)),
            cancellationToken);

        return Result.Success(new SendEnvelopeResult(envelope.Id, dispatched));
    }
}
