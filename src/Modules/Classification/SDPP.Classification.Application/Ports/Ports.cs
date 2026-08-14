using SDPP.Classification.Domain.Aggregates;

namespace SDPP.Classification.Application.Ports;

/// <summary>
/// Fetches the extractable text + filename/metadata of a document from the Documents module, so
/// detectors never need to know how to parse Office/PDF binaries themselves (see
/// docs/05-security/classification-engine.md §3.1). Implemented as an HTTP client back to
/// Document API's content endpoint in Infrastructure.
/// </summary>
public interface IDocumentContentClient
{
    Task<DocumentContentForInspection> GetContentForInspectionAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public sealed record DocumentContentForInspection(string FileName, string ExtractedText, IReadOnlyDictionary<string, string> Metadata);

public interface IDlpRuleRepository
{
    Task<IReadOnlyList<DlpRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default);
    void Add(DlpRule rule);
}

public interface IClassificationPolicyRepository
{
    Task<IReadOnlyList<ClassificationPolicy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default);
    void Add(ClassificationPolicy policy);
}

public interface IInspectionResultRepository
{
    Task<InspectionResult?> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    void Add(InspectionResult inspectionResult);
}

public interface IDocumentIntegrityRecordRepository
{
    Task<DocumentIntegrityRecord?> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Exact-match lookup by the document's SHA-256 hash — backs the audit trail's
    /// "search by hash" flow (find the document a given hash belongs to, then query its audit
    /// records by DocumentId). Sha256Hash.Value is indexed (see DocumentIntegrityRecordConfiguration).</summary>
    Task<DocumentIntegrityRecord?> GetByHashAsync(string sha256Hash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentIntegrityRecord>> GetByDocumentIdsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken = default);

    /// <summary>Most recently registered instance of a given DocumentVersion — used to get a
    /// comparable physical hash/content type when a fingerprint match is found under a *different*
    /// DocumentVersion (see IChangeDetectionService).</summary>
    Task<DocumentIntegrityRecord?> GetLatestByDocumentVersionIdAsync(Guid documentVersionId, CancellationToken cancellationToken = default);

    void Add(DocumentIntegrityRecord record);
}

public interface IDocumentVersionFingerprintRepository
{
    Task<DocumentVersionFingerprint?> GetByDocumentVersionIdAsync(Guid documentVersionId, CancellationToken cancellationToken = default);

    /// <summary>The most recent DocumentVersionFingerprint (anywhere on the platform) with this
    /// exact ContentFingerprint, excluding versions that don't have one yet — lets a completely
    /// independent re-upload of previously-classified content skip reclassification. See
    /// ClassifyDocumentHandler.</summary>
    Task<DocumentVersionFingerprint?> GetLatestByContentFingerprintAsync(string contentFingerprint, CancellationToken cancellationToken = default);

    void Add(DocumentVersionFingerprint fingerprint);
}
