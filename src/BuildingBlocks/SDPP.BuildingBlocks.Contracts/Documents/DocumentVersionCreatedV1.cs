namespace SDPP.BuildingBlocks.Contracts.Documents;

/// <summary>
/// Published whenever a DocumentInstance's DocumentVersion gets a fingerprint and classification
/// for the first time — whether that classification was freshly computed by Classification.Api
/// (ChangeType "Initial") or inherited from a previously-seen DocumentVersion with the same
/// content fingerprint (any other ChangeType — see IChangeDetectionService). Covers the audit
/// requirement of recording old/new fingerprint and old/new classification for every content
/// decision, not just successful conversions (see the integrity-and-fingerprint proposal,
/// "Auditoría y cadena de custodia").
/// </summary>
public sealed record DocumentVersionCreatedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid DocumentId,
    Guid LogicalDocumentId,
    Guid DocumentVersionId,
    int VersionNumber,
    string ChangeType,
    string? PreviousContentFingerprint,
    string NewContentFingerprint,
    Guid? InheritedFromVersionId,
    string? PreviousClassification,
    string NewClassification,
    ActorSnapshot Actor) : IIntegrationEvent;
