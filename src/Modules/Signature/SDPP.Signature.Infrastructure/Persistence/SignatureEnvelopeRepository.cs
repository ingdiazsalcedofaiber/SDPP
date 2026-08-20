using Microsoft.EntityFrameworkCore;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Infrastructure.Persistence;

public sealed class SignatureEnvelopeRepository(SignatureDbContext dbContext) : ISignatureEnvelopeRepository
{
    private static readonly EnvelopeStatus[] TerminalStatuses =
        [EnvelopeStatus.Completed, EnvelopeStatus.Declined, EnvelopeStatus.Cancelled, EnvelopeStatus.Expired];

    private static readonly RecipientStatus[] ActionableRecipientStatuses = [RecipientStatus.Sent, RecipientStatus.Viewed];

    public Task<SignatureEnvelope?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<SignatureEnvelope?> GetByRecipientIdAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(e => e.Recipients.Any(r => r.Id == recipientId), cancellationToken);

    public async Task<IReadOnlyList<SignatureEnvelope>> SearchAsync(Guid userId, string scope, Guid organizationId, CancellationToken cancellationToken = default)
    {
        // "pending" also covers a creator's own unfinished firma-rápida (InPerson) sessions — those
        // never match `r.MatchedUserId == userId` (the patient has no SDPP account), so without this
        // second clause they'd only ever show up under "sent"/"all", never in the "cosas que tengo
        // pendientes" tab the way a matched-recipient turn does. See ListEnvelopesHandler for how the
        // two cases get told apart again once they're both in the result set.
        var query = Query().Where(e => e.OrganizationId == organizationId);
        query = scope switch
        {
            "sent" => query.Where(e => e.CreatedByUserId == userId),
            "pending" => query.Where(e =>
                !TerminalStatuses.Contains(e.Status) &&
                (e.Recipients.Any(r => r.MatchedUserId == userId && ActionableRecipientStatuses.Contains(r.Status)) ||
                 (e.CreatedByUserId == userId && e.Recipients.Any(r => r.InPerson && ActionableRecipientStatuses.Contains(r.Status))))),
            _ => query.Where(e =>
                e.CreatedByUserId == userId ||
                (!TerminalStatuses.Contains(e.Status) && e.Recipients.Any(r => r.MatchedUserId == userId && ActionableRecipientStatuses.Contains(r.Status)))),
        };

        return await query.OrderByDescending(e => e.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SignatureEnvelope>> GetPastDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(e => e.DueDateUtc != null && e.DueDateUtc < asOfUtc && !TerminalStatuses.Contains(e.Status))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SignatureEnvelope>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        await Query().Where(e => !TerminalStatuses.Contains(e.Status)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SignatureEnvelope>> GetInvolvingUserAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(e => e.OrganizationId == organizationId && (e.CreatedByUserId == userId || e.Recipients.Any(r => r.MatchedUserId == userId)))
            .ToListAsync(cancellationToken);

    public void Add(SignatureEnvelope envelope) => dbContext.SignatureEnvelopes.Add(envelope);

    private IQueryable<SignatureEnvelope> Query() =>
        dbContext.SignatureEnvelopes.Include(e => e.Recipients).Include(e => e.Fields)
            .Include(e => e.ConsentRecords).Include(e => e.DocumentSignatures);
}
