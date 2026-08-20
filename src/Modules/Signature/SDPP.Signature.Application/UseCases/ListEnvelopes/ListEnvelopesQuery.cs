using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;


namespace SDPP.Signature.Application.UseCases.ListEnvelopes;

public sealed record EnvelopeSummaryDto(
    Guid EnvelopeId, string Title, string Status, string SigningMode, int RecipientCount, int SignedCount,
    DateTime CreatedAtUtc, DateTime? DueDateUtc, DateTime? CompletedAtUtc,
    // The two ways an envelope can land in scope="pending" (see SignatureEnvelopeRepository.SearchAsync)
    // — never both true for the same envelope in practice (a firma-rápida recipient has no SDPP
    // account to match), kept as two separate flags rather than one enum so the frontend's "Todos"
    // tab (which doesn't care about either) can just ignore them.
    bool IsMyTurnToSign, bool HasPendingInPersonSignature);

/// <summary>Backs "Bandeja de firmas". scope: "sent" (I created these), "pending" (I'm a recipient
/// and it's my turn), "all" (either).</summary>
public sealed record ListEnvelopesQuery(string Scope) : IQuery<IReadOnlyList<EnvelopeSummaryDto>>;

public sealed class ListEnvelopesHandler(
    ISignatureEnvelopeRepository repository, ICurrentActor currentActor, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<ListEnvelopesQuery, Result<IReadOnlyList<EnvelopeSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<EnvelopeSummaryDto>>> Handle(ListEnvelopesQuery request, CancellationToken cancellationToken)
    {
        var envelopes = await repository.SearchAsync(
            currentActor.UserId, request.Scope, organizationContextProvider.GetCurrentOrganizationId(), cancellationToken);

        bool IsActionable(RecipientStatus status) => status is RecipientStatus.Sent or RecipientStatus.Viewed;

        var summaries = envelopes
            .Select(e => new EnvelopeSummaryDto(
                e.Id, e.Title, e.Status.ToString(), e.SigningMode.ToString(), e.Recipients.Count,
                e.Recipients.Count(r => r.Status == RecipientStatus.Signed), e.CreatedAtUtc, e.DueDateUtc, e.CompletedAtUtc,
                IsMyTurnToSign: e.Recipients.Any(r => r.MatchedUserId == currentActor.UserId && IsActionable(r.Status)),
                HasPendingInPersonSignature: e.CreatedByUserId == currentActor.UserId && e.Recipients.Any(r => r.InPerson && IsActionable(r.Status))))
            .ToList();

        return Result.Success<IReadOnlyList<EnvelopeSummaryDto>>(summaries);
    }
}
