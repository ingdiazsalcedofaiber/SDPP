namespace SDPP.BuildingBlocks.Contracts.Documents;

/// <summary>
/// Published when the policy engine blocks a conversion request — either at RequestConversionHandler
/// (category/classification rule, e.g. "Historia Clínica no puede convertirse a DOCX") or from the
/// worker's own blockEditableConversion check for Secreta-level documents. More specific than the
/// generic DocumentBlockedV1 (which stays for inspection-time blocks), so audit records retain the
/// operation/category context a block reason alone doesn't carry.
/// </summary>
public sealed record ConversionBlockedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid DocumentId,
    Guid? JobId,
    string OperationType,
    string Classification,
    string? Category,
    string Reason,
    ActorSnapshot Actor) : IIntegrationEvent;

/// <summary>Published by SDPP.Conversion.Worker after ProtectionEngine stamps a conversion output —
/// see docs on automatic protection, "Registro de auditoría". DocumentVersionId/OutputContentType
/// let Classification's ProtectionAppliedIntegrityConsumer create the DocumentIntegrityRecord
/// itself if this event happens to arrive before ConversionCompletedV1 (bus delivery order between
/// two independently-published event types is never guaranteed) — see the "Clasificación de
/// Activos de Información" extraction.</summary>
public sealed record ProtectionAppliedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid AuditId,
    Guid DocumentId,
    Guid JobId,
    Guid OutputDocumentId,
    string Classification,
    string? Category,
    IReadOnlyList<string> Labels,
    int RiskScore,
    IReadOnlyList<string> ProtectionsApplied,
    string OutputSha256Hash,
    string? IntegritySignature,
    Guid DocumentVersionId,
    string OutputContentType) : IIntegrationEvent;

/// <summary>Published by AuditLoggingNotificationSender (the default INotificationSender) so an
/// Altamente Sensible/Secreta protection event is at least recorded in the audit trail even before
/// a real email/Teams/Slack channel is wired up — see the notification scope decision in the
/// approved plan.</summary>
public sealed record AdminNotificationRequestedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid DocumentId,
    Guid AuditId,
    string Subject,
    string Body) : IIntegrationEvent;
