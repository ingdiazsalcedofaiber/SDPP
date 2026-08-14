namespace SDPP.BuildingBlocks.Contracts.Documents;

/// <summary>Published by the Documents module right after a file is stored and hashed.
/// DocumentVersionId lets Classification's DocumentUploadedIntegrityConsumer register a
/// DocumentIntegrityRecord it can later look up by version (see the "Clasificación de Activos de
/// Información" extraction).</summary>
public sealed record DocumentUploadedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid DocumentId,
    Guid OwnerId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    ActorSnapshot Actor,
    Guid DocumentVersionId) : IIntegrationEvent;

/// <summary>
/// Immutable snapshot of who performed the action and from where, captured at the time of the
/// request — the raw material for the audit trail (see
/// docs/05-security/audit-and-traceability.md §1).
/// </summary>
public sealed record ActorSnapshot(
    Guid UserId,
    string FullName,
    string Email,
    string Domain,
    string? IpAddress,
    string? Hostname,
    string? OperatingSystem,
    string? UserAgent,
    string? MacAddress);
