using SDPP.BuildingBlocks.Domain;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Domain.Aggregates;

/// <summary>
/// One row per Documents-module <c>DocumentVersion</c> — the "current decision state" for a
/// version's content fingerprint and classification, moved here from Documents as part of the
/// "Clasificación de Activos de Información" extraction. Deliberately separate from
/// <see cref="InspectionResult"/> (an append-only log of every inspection run): this is the
/// single current answer for "what is this version's fingerprint/classification right now",
/// while InspectionResult keeps the full history of how it got there.
/// </summary>
public sealed class DocumentVersionFingerprint : AggregateRoot<Guid>
{
    public Guid DocumentVersionId { get; private set; }
    public Guid LogicalDocumentId { get; private set; }

    /// <summary>SHA-256 over the normalized extracted text — exact-match signal, survives format
    /// conversion as long as extraction is deterministic. Null until the first successful text
    /// extraction.</summary>
    public string? ContentFingerprint { get; private set; }

    /// <summary>64-bit SimHash (hex) over 5-word shingles of the same normalized text —
    /// near-duplicate signal, used when ContentFingerprint doesn't match exactly.</summary>
    public string? StructuralSignature { get; private set; }

    public ClassificationLevel Classification { get; private set; }
    public ClassificationSource ClassificationSource { get; private set; }
    public int? RiskScore { get; private set; }
    public string? Category { get; private set; }
    public IReadOnlyList<string> Labels { get; private set; } = [];

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private DocumentVersionFingerprint() { } // EF Core

    public static DocumentVersionFingerprint Create(Guid documentVersionId, Guid logicalDocumentId)
    {
        var now = DateTime.UtcNow;
        return new DocumentVersionFingerprint
        {
            Id = Guid.NewGuid(),
            DocumentVersionId = documentVersionId,
            LogicalDocumentId = logicalDocumentId,
            ClassificationSource = ClassificationSource.Manual,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void SetFingerprint(string contentFingerprint, string structuralSignature)
    {
        ContentFingerprint = contentFingerprint;
        StructuralSignature = structuralSignature;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ApplyClassification(
        ClassificationLevel classification, ClassificationSource source,
        int? riskScore, string? category, IReadOnlyList<string>? labels)
    {
        Classification = classification;
        ClassificationSource = source;
        RiskScore = riskScore;
        Category = category;
        Labels = labels ?? [];
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Copies the classification of the version this one inherited from — used whenever
    /// a fingerprint match is found elsewhere on the platform.</summary>
    public void InheritClassificationFrom(DocumentVersionFingerprint previous) =>
        ApplyClassification(previous.Classification, previous.ClassificationSource, previous.RiskScore, previous.Category, previous.Labels);
}
