namespace SDPP.Classification.Domain.Enums;

/// <summary>
/// Deliberately a separate type from Documents.Domain.Enums.ClassificationLevel — each bounded
/// context owns its own model (docs/02-domain/domain-model.md §1); the two are kept in sync by
/// the published-language contract (string names) rather than a shared assembly reference.
/// </summary>
public enum ClassificationLevel
{
    Publica = 0,
    UsoInterno = 1,
    Privada = 2,
    Confidencial = 3,
    Restringida = 4,
    Secreta = 5,
}

/// <summary>How a DocumentVersionFingerprint's classification was determined — mirrors
/// Documents.Domain.Enums.ClassificationSource (kept as an independent copy, same deliberate
/// per-bounded-context duplication as ClassificationLevel itself).</summary>
public enum ClassificationSource
{
    Manual = 0,
    Automatic = 1,
    Hybrid = 2,
}

public enum FindingCategory
{
    PII,
    Financial,
    Medical,
    Legal,
    SourceCode,
    IntellectualProperty,
    Credentials,
    Strategic,
}

public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum InspectionTrigger
{
    PreConversion,
    PostConversion,
    Scheduled,
}

public enum InspectionStatus
{
    Pending,
    Completed,
    Overridden,
    Failed,
}

public enum DetectorType
{
    Regex,
    Keyword,
    Checksum,
    Dictionary,
    MLModel,
    ExternalDlpEngine,
}

public enum PolicyEffect
{
    Allow,
    Block,
    RequireApproval,
}

/// <summary>What changed between a new document instance and the DocumentVersionFingerprint it's
/// being compared against (see IChangeDetectionService) — drives whether the classification
/// engine re-runs. Moved here from Documents.Domain.Enums as part of the "Clasificación de
/// Activos de Información" extraction; Documents keeps its own copy on DocumentVersion for its
/// own version-lineage bookkeeping (deliberate duplication, same as ClassificationLevel).</summary>
public enum ChangeType
{
    /// <summary>No previous version exists yet — always classify.</summary>
    Initial,

    /// <summary>Byte-identical re-upload — nothing to do at all.</summary>
    None,

    /// <summary>Same content fingerprint, same format — only non-textual metadata changed
    /// (author, save timestamp, embedded revision id).</summary>
    MetadataOnly,

    /// <summary>Same content fingerprint, different format — a real Word→PDF-style conversion.</summary>
    FormatConversion,

    /// <summary>Fingerprints don't match exactly but the structural signature is within the
    /// near-duplicate threshold — almost certainly extraction noise (OCR, ligatures) from a
    /// format conversion, not a real edit.</summary>
    FormatConversionNoise,

    /// <summary>Structural signature indicates a real but limited edit — reclassifies, but can
    /// never silently lower the classification.</summary>
    PartialModification,

    /// <summary>Structural signature indicates the content is substantially different — full,
    /// unconditional reclassification.</summary>
    TotalModification,
}
