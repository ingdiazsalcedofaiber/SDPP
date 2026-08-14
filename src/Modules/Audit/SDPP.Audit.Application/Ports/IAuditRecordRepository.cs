using SDPP.Audit.Domain.Aggregates;

namespace SDPP.Audit.Application.Ports;

public sealed record AuditQueryFilter(
    Guid? DocumentId, Guid? UserId, DateTime? FromUtc, DateTime? ToUtc, string? EventType, int Page, int PageSize);

public sealed record AuditQueryPage(IReadOnlyList<AuditRecord> Items, int TotalCount, bool ChainValid);

public interface IAuditRecordRepository
{
    /// <summary>Reads the RecordHash of the most recently inserted row — the anchor for the next
    /// record in the chain. Must be called under a lock/transaction isolation that serializes
    /// concurrent writers (see docs/05-security/audit-and-traceability.md §2).</summary>
    Task<string> GetLastRecordHashAsync(CancellationToken cancellationToken = default);

    void Add(AuditRecord record);

    Task<AuditQueryPage> QueryAsync(AuditQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Every record whose SubjectDocumentId is one of the given ids, ordered by Id
    /// ascending — backs VerifyIntegrityQuery, which needs the full relevant slice (not a page) to
    /// check each record's global chain linkage, not just its own hash.</summary>
    Task<IReadOnlyList<AuditRecord>> GetBySubjectIdsAsync(IReadOnlyList<Guid> subjectIds, CancellationToken cancellationToken = default);

    /// <summary>Fetches one record by its primary key — used to look up a candidate's immediate
    /// GLOBAL predecessor (Id - 1) when verifying chain linkage, since the predecessor by Id is not
    /// necessarily itself a record about the same subject.</summary>
    Task<AuditRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
