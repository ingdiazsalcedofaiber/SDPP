using SDPP.BuildingBlocks.Domain;
using SDPP.Classification.Domain.ValueObjects;

namespace SDPP.Classification.Domain.Aggregates;

/// <summary>
/// One row per physical <c>DocumentInstance</c> (Documents module — both original uploads and
/// every conversion output get their own row, matching the fact that a DOCX and its PDF
/// conversion share one DocumentVersionId but have different bytes/signatures/stamps). Populated
/// asynchronously from the Documents/Worker-published integration events
/// (DocumentUploadedV1/ConversionCompletedV1/ProtectionAppliedV1) — see
/// Classification.Infrastructure/Messaging — since hashing/protection still execute where the
/// file bytes already are; this module owns what the resulting values mean and where they live.
/// </summary>
public sealed class DocumentIntegrityRecord : AggregateRoot<Guid>
{
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public string ContentType { get; private set; } = null!;
    public FileHash Sha256Hash { get; private set; } = null!;

    /// <summary>HMAC-SHA256 over this document's stored bytes, set only for outputs the moved
    /// ProtectionEngine actually stamped.</summary>
    public string? IntegritySignature { get; private set; }

    /// <summary>Which protections were actually applied (e.g. "Watermark", "Footer",
    /// "IntegritySignature") — set only on conversion outputs.</summary>
    public IReadOnlyList<string> ProtectionsApplied { get; private set; } = [];

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private DocumentIntegrityRecord() { } // EF Core

    public static DocumentIntegrityRecord Register(
        Guid documentId, Guid documentVersionId, string contentType, FileHash sha256Hash)
    {
        var now = DateTime.UtcNow;
        return new DocumentIntegrityRecord
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            DocumentVersionId = documentVersionId,
            ContentType = contentType,
            Sha256Hash = sha256Hash,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void SetIntegritySignature(string signature)
    {
        IntegritySignature = signature;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetProtectionsApplied(IReadOnlyList<string> protectionsApplied)
    {
        ProtectionsApplied = protectionsApplied;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
