namespace SDPP.BuildingBlocks.Contracts.Documents;

/// <summary>Published by SDPP.Conversion.Worker when a job finishes successfully.
/// DocumentVersionId/OutputContentType let Classification's ConversionCompletedIntegrityConsumer
/// register a DocumentIntegrityRecord for the output instance (see the "Clasificación de Activos
/// de Información" extraction) — a worker-driven conversion always attaches to the same
/// DocumentVersion as its source, never a new one.</summary>
public sealed record ConversionCompletedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid JobId,
    Guid DocumentId,
    Guid OutputDocumentId,
    string OutputSha256Hash,
    string EngineUsed,
    int DurationMs,
    string FinalClassification,
    Guid DocumentVersionId,
    string OutputContentType) : IIntegrationEvent;

/// <summary>Published by SDPP.Conversion.Worker when a job fails (engine error, timeout, malware found, etc.).</summary>
public sealed record ConversionFailedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid JobId,
    Guid DocumentId,
    string ErrorCode,
    string ErrorDetail) : IIntegrationEvent;

/// <summary>Published by the Documents module when a document is blocked (policy, malware, inspection failure).</summary>
public sealed record DocumentBlockedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid DocumentId,
    string Reason,
    string Classification) : IIntegrationEvent;
