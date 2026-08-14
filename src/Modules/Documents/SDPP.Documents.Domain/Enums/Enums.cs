namespace SDPP.Documents.Domain.Enums;

/// <summary>
/// Ordered sensitivity levels — comparable by design so policies can express "at least
/// Confidencial" without special-casing (see docs/05-security/classification-engine.md §1).
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

public enum DocumentStatus
{
    Uploaded = 0,
    Inspecting = 1,
    Blocked = 2,
    Ready = 3,
    Archived = 4,
    PendingDeletion = 5,
    Deleted = 6,

    /// <summary>Terminal, enforced read-only state — set only on the final artifact produced by a
    /// completed signature envelope (never on the original source document). Appended at the end,
    /// not inserted, so the persisted int values for every earlier status stay stable. See
    /// RequestConversionHandler and CreateSignedVersionHandler for the guard.</summary>
    Locked = 7,
}

public enum ConversionJobStatus
{
    PendingForm = 0,
    Queued = 1,
    Inspecting = 2,
    Approved = 3,
    AwaitingApproval = 4,
    Rejected = 5,
    Processing = 6,
    Completed = 7,
    Failed = 8,
}

/// <summary>Every operation type supported by the platform (see docs/04-use-cases/use-cases.md §2).
/// Every value has a real engine registered in SDPP.Documents.Infrastructure/Engines except
/// <see cref="DigitalSign"/>, deliberately: signing with a fake/self-signed certificate would
/// produce a signature that *looks* valid in the UI while being cryptographically meaningless —
/// actively worse than not offering the feature. It requires a real PKI integration
/// (docs/07-operations/kubernetes-architecture.md) before going live, and is not exposed in the
/// frontend until then. Requesting it fails cleanly with "no hay un motor de conversión
/// registrado" (ConversionRequestedConsumer's NotSupportedException), the same as any other
/// operation with no matching IConversionEngine.CanHandle.</summary>
public enum OperationType
{
    WordToPdf,
    ExcelToPdf,
    PptToPdf,
    ImageToPdf,
    PdfToWord,
    PdfToExcel,
    PdfToImage,
    PdfToPpt,
    Merge,
    Split,
    Compress,
    Ocr,
    Watermark,
    PageNumbering,
    Rotate,
    DigitalSign,
    DeletePages,
    ReorderPages,
    Protect,
    Unlock,
}

/// <summary>What actually changed between a new DocumentInstance and the DocumentVersion it's
/// being compared against (see IChangeDetectionService) — drives whether the classification
/// engine re-runs. Only PartialModification/TotalModification (and the implicit first-ever
/// Initial) ever trigger a real inspection; everything else inherits the previous version's
/// classification untouched.</summary>
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
    /// never silently lower the classification (see RequestConversionHandler).</summary>
    PartialModification,

    /// <summary>Structural signature indicates the content is substantially different — full,
    /// unconditional reclassification.</summary>
    TotalModification,
}
