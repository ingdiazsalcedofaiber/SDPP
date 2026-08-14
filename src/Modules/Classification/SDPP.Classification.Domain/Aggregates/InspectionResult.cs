using SDPP.BuildingBlocks.Domain;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Domain.Aggregates;

public sealed class Finding : Entity<Guid>
{
    public string DetectorId { get; private set; } = null!;
    public FindingCategory Category { get; private set; }
    public Severity Severity { get; private set; }
    public int MatchCount { get; private set; }
    public string Location { get; private set; } = null!;
    public string RuleVersion { get; private set; } = null!;

    /// <summary>Copied from the DlpRule that produced this finding at match time, so
    /// RiskScoringEngine/LabelEngine can aggregate without a second lookup — see
    /// Classification.Application/RiskScoringEngine.cs and LabelEngine.cs.</summary>
    public int Weight { get; private set; }
    public IReadOnlyList<string> Labels { get; private set; } = [];
    public string? BusinessCategory { get; private set; }

    private Finding() { } // EF Core

    public static Finding Create(
        string detectorId, FindingCategory category, Severity severity, int matchCount, string location, string ruleVersion,
        int weight = 0, IReadOnlyList<string>? labels = null, string? businessCategory = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            DetectorId = detectorId,
            Category = category,
            Severity = severity,
            MatchCount = matchCount,
            Location = location,
            RuleVersion = ruleVersion,
            Weight = weight,
            Labels = labels ?? [],
            BusinessCategory = businessCategory,
        };
}

/// <summary>
/// Aggregate root produced by the classification engine (docs/05-security/classification-engine.md
/// §3). The mapping from worst-finding-severity to a suggested classification lives here as a
/// domain rule, not scattered across handlers. RiskScore/Labels/BusinessCategory are computed by
/// the Application-layer RiskScoringEngine/LabelEngine (separate, independently testable pipeline
/// stages — see docs/05-security/dlp-engine.md §4) and handed in here for the aggregate to own as
/// its final, persisted state, the same division of responsibility already used for
/// SuggestedClassification itself.
/// </summary>
public sealed class InspectionResult : AggregateRoot<Guid>
{
    private readonly List<Finding> _findings = [];

    public Guid DocumentId { get; private set; }
    public InspectionTrigger TriggeredBy { get; private set; }
    public ClassificationLevel SuggestedClassification { get; private set; }
    public ClassificationLevel FinalClassification { get; private set; }
    public bool RequiresManualReview { get; private set; }
    public InspectionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public int RiskScore { get; private set; }
    public IReadOnlyList<string> Labels { get; private set; } = [];
    public string? BusinessCategory { get; private set; }

    public IReadOnlyList<Finding> Findings => _findings.AsReadOnly();

    private InspectionResult() { } // EF Core

    public static InspectionResult Start(Guid documentId, InspectionTrigger trigger) => new()
    {
        Id = Guid.NewGuid(),
        DocumentId = documentId,
        TriggeredBy = trigger,
        Status = InspectionStatus.Pending,
        CreatedAtUtc = DateTime.UtcNow,
    };

    /// <summary>Severity → classification mapping from docs/05-security/classification-engine.md §3.2.</summary>
    public void Complete(IReadOnlyCollection<Finding> findings, int riskScore, IReadOnlyList<string> labels, string? businessCategory)
    {
        _findings.AddRange(findings);

        var worstSeverity = findings.Count == 0 ? Severity.Info : findings.Max(f => f.Severity);
        SuggestedClassification = worstSeverity switch
        {
            Severity.Critical => ClassificationLevel.Secreta,
            Severity.High => ClassificationLevel.Restringida,
            Severity.Medium => ClassificationLevel.Confidencial,
            Severity.Low => ClassificationLevel.Privada,
            _ => ClassificationLevel.UsoInterno,
        };

        FinalClassification = SuggestedClassification;
        RequiresManualReview = worstSeverity >= Severity.Critical;
        RiskScore = riskScore;
        Labels = labels;
        BusinessCategory = businessCategory;
        Status = InspectionStatus.Completed;
    }

    public void Override(ClassificationLevel finalClassification, string reason)
    {
        if (finalClassification < SuggestedClassification)
        {
            throw new DomainException(
                "No se puede bajar la clasificación sugerida sin el permiso 'classification.override.downgrade' " +
                "(ver docs/05-security/rbac-matrix.md §4).");
        }

        FinalClassification = finalClassification;
        RequiresManualReview = false;
        Status = InspectionStatus.Overridden;
    }

    public void Fail() => Status = InspectionStatus.Failed;
}
