namespace SDPP.Signature.Application.Ports;

public sealed record AuditIntegrityCheck(bool IsIntact, int RecordCount);

public sealed record AuditTrailRecord(
    long Id, DateTime OccurredAtUtc, string EventType, string ActorFullName, string ActorEmail, string? ActorIp,
    Guid? SubjectDocumentId, string PayloadJson, string PreviousRecordHash, string RecordHash);

/// <summary>Signature's outbound port to Audit.Api — used by the public envelope verifier to back
/// its "auditoría íntegra" claim with a real check of Audit's own hash-chained records (never
/// trusting anything Signature itself stores), and by the Evidence Package export to include the
/// real audit trail. Signature never touches SDPP_Audit directly.</summary>
public interface IAuditClient
{
    Task<AuditIntegrityCheck> VerifyIntegrityAsync(IReadOnlyList<Guid> subjectIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditTrailRecord>> GetRecordsAsync(IReadOnlyList<Guid> subjectIds, CancellationToken cancellationToken = default);
}
