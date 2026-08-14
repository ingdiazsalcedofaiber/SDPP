using Microsoft.EntityFrameworkCore;
using SDPP.Audit.Application.Ports;
using SDPP.Audit.Domain.Aggregates;

namespace SDPP.Audit.Infrastructure.Persistence;

public sealed class AuditRecordRepository(AuditDbContext dbContext) : IAuditRecordRepository
{
    /// <summary>
    /// Opens (and keeps open on the DbContext) the transaction that will also cover the
    /// subsequent insert, then reads the last RecordHash with an UPDLOCK/ROWLOCK hint so
    /// concurrent writers serialize on this single row instead of racing to build the chain —
    /// see docs/05-security/audit-and-traceability.md §2. AuditEfUnitOfWork.SaveChangesAsync
    /// commits and disposes the transaction after the insert.
    /// </summary>
    public async Task<string> GetLastRecordHashAsync(CancellationToken cancellationToken = default)
    {
        dbContext.PendingChainTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var lastHash = await dbContext.AuditRecords
            .FromSqlRaw("SELECT TOP 1 * FROM audit.AuditRecords WITH (UPDLOCK, ROWLOCK) ORDER BY Id DESC")
            .Select(r => r.RecordHash)
            .FirstOrDefaultAsync(cancellationToken);

        return lastHash ?? AuditRecord.GenesisHash;
    }

    public void Add(AuditRecord record) => dbContext.AuditRecords.Add(record);

    public async Task<AuditQueryPage> QueryAsync(AuditQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AuditRecords.AsNoTracking().AsQueryable();

        if (filter.DocumentId is not null) query = query.Where(r => r.SubjectDocumentId == filter.DocumentId);
        if (filter.UserId is not null) query = query.Where(r => r.ActorUserId == filter.UserId);
        if (filter.FromUtc is not null) query = query.Where(r => r.OccurredAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(r => r.OccurredAtUtc <= filter.ToUtc);
        if (!string.IsNullOrWhiteSpace(filter.EventType)) query = query.Where(r => r.EventType == filter.EventType);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var chainValid = VerifyChainSegment(items);

        return new AuditQueryPage(items, totalCount, chainValid);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetBySubjectIdsAsync(IReadOnlyList<Guid> subjectIds, CancellationToken cancellationToken = default) =>
        await dbContext.AuditRecords.AsNoTracking()
            .Where(r => r.SubjectDocumentId != null && subjectIds.Contains(r.SubjectDocumentId!.Value))
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

    public Task<AuditRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        dbContext.AuditRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <summary>Cheap, page-local integrity check. The full-chain verification job described in
    /// docs/05-security/audit-and-traceability.md §2 runs separately as a Hangfire job over the
    /// entire table.</summary>
    private static bool VerifyChainSegment(IReadOnlyList<AuditRecord> itemsDescending)
    {
        for (var i = 0; i < itemsDescending.Count - 1; i++)
        {
            if (itemsDescending[i].PreviousRecordHash != itemsDescending[i + 1].RecordHash)
            {
                return false;
            }
        }
        return itemsDescending.All(r => r.VerifyHash());
    }
}
