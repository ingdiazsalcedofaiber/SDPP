namespace SDPP.BuildingBlocks.Contracts.Classification;

/// <summary>Published by the Classification module once automatic inspection finishes (see
/// docs/05-security/classification-engine.md §3).</summary>
public sealed record InspectionCompletedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid InspectionId,
    Guid DocumentId,
    string SuggestedClassification,
    bool RequiresManualReview,
    IReadOnlyCollection<FindingSummary> Findings) : IIntegrationEvent;

public sealed record FindingSummary(
    string DetectorId,
    string Category,
    string Severity,
    int MatchCount);

/// <summary>
/// Published whenever a finding of severity High or Critical is confirmed — the trigger for the
/// alerting fan-out described in docs/05-security/audit-and-traceability.md §5.
/// </summary>
public sealed record SensitiveDataDetectedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid DocumentId,
    string Classification,
    IReadOnlyCollection<FindingSummary> Findings) : IIntegrationEvent;

/// <summary>Published when the policy engine decides a ConversionJob requires supervisor approval.</summary>
public sealed record ApprovalRequiredV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid ApprovalRequestId,
    Guid JobId,
    Guid DocumentId,
    string Area,
    string RequiredApproverRole,
    DateTime ExpiresAtUtc) : IIntegrationEvent;
