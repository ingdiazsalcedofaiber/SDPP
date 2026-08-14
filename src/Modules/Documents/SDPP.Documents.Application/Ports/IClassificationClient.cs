using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Application.Ports;

public sealed record ClassifyDocumentDecision(
    ClassificationLevel Classification, string ClassificationSource, bool RequiresManualReview,
    int RiskScore, string? Category, IReadOnlyList<string> Labels,
    string? ContentFingerprint, string? StructuralSignature, string ChangeType, Guid? InheritedFromVersionId);

public sealed record VersionSummary(ClassificationLevel? Classification, int? RiskScore, string? Category, IReadOnlyList<string> Labels);

public sealed record DocumentIntegritySummary(Guid DocumentId, string? IntegritySignature, IReadOnlyList<string> ProtectionsApplied);

/// <summary>
/// Synchronous port to the Classification & DLP module — now also "Clasificación de Activos de
/// Información" (hashing/fingerprinting/watermarking/integrity, see the module extraction). The
/// policy evaluation gate (block/require-approval) is Classification's own capability, still
/// available at POST /api/v1/classification/policies/evaluate, but the Panel de Conversión
/// simplification stopped calling it from here — RequestConversionHandler no longer gates
/// conversions on it (see the approved plan).
/// </summary>
public interface IClassificationClient
{
    /// <summary>Fingerprints/classifies a document — replaces what used to be local
    /// fingerprint-then-inspect orchestration inside RequestConversionHandler. Classification.Api
    /// decides internally whether this is genuinely new content (runs a real inspection) or a
    /// fingerprint match with content seen elsewhere on the platform (inherits). Still called
    /// automatically so the Worker's existing automatic protection/watermark step keeps working —
    /// it's just no longer shown or gated on in the conversion panel.</summary>
    Task<ClassifyDocumentDecision> ClassifyAsync(
        Guid documentId, Guid documentVersionId, Guid logicalDocumentId, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Server-side enrichment for GetDocumentStatusHandler — lets Documents.Api keep
    /// returning classification/risk/category/labels in its own response shape without storing
    /// any of it locally.</summary>
    Task<VersionSummary> GetVersionSummaryAsync(Guid documentVersionId, CancellationToken cancellationToken = default);

    /// <summary>Batched (avoids N+1) integrity-signature/protections-applied lookup for every job
    /// output in a document's status response.</summary>
    Task<IReadOnlyList<DocumentIntegritySummary>> GetBatchIntegrityAsync(
        IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken = default);
}
