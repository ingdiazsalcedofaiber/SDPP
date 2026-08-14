using SDPP.BuildingBlocks.Domain;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Domain.Aggregates;

/// <summary>
/// A single configurable detection rule (see docs/05-security/dlp-engine.md §4). The
/// application-layer detector registry (Classification.Application/Detectors) instantiates the
/// concrete IDetector strategy matching DetectorType + PatternOrConfigJson at runtime — this
/// aggregate only owns the rule's data and lifecycle, not the detection algorithm itself.
/// </summary>
public sealed class DlpRule : AggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;
    public DetectorType DetectorType { get; private set; }
    public string PatternOrConfigJson { get; private set; } = null!;
    public FindingCategory Category { get; private set; }
    public Severity DefaultSeverity { get; private set; }
    public bool Enabled { get; private set; }
    public int Version { get; private set; }

    /// <summary>Contribution to a document's aggregate RiskScore per match (RiskScoringEngine
    /// multiplies this by match count) — see docs/05-security/dlp-engine.md §4. Configurable per
    /// rule rather than derived purely from Severity, so two Critical rules can still carry
    /// different weight (e.g. a national ID is riskier in bulk than a single email address).</summary>
    public int Weight { get; private set; }

    /// <summary>Free-form tags this rule contributes to a document's aggregate Labels (e.g.
    /// "PII", "MEDICO", "HISTORIA_CLINICA") — purely descriptive, never used for policy matching
    /// (see BusinessCategory for that).</summary>
    public IReadOnlyList<string> Labels { get; private set; } = [];

    /// <summary>Business-domain category this rule identifies (e.g. "HistoriaClinica",
    /// "ContratoLegal") — distinct from the technical <see cref="FindingCategory"/>. Null for
    /// rules that only contribute risk/labels without asserting a specific document category.
    /// This is what ClassificationPolicy.PolicyRule.ConditionCategory matches against.</summary>
    public string? BusinessCategory { get; private set; }

    private DlpRule() { } // EF Core

    public static DlpRule Create(
        string name, DetectorType detectorType, string patternOrConfigJson, FindingCategory category, Severity defaultSeverity,
        int? weight = null, IReadOnlyList<string>? labels = null, string? businessCategory = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        DetectorType = detectorType,
        PatternOrConfigJson = patternOrConfigJson,
        Category = category,
        DefaultSeverity = defaultSeverity,
        Weight = weight ?? DefaultWeightFor(defaultSeverity),
        Labels = labels ?? [],
        BusinessCategory = businessCategory,
        Enabled = true,
        Version = 1,
    };

    /// <summary>Sane default so existing/simple rules don't need an explicit weight — a rule can
    /// still override it for finer-grained risk tuning.</summary>
    private static int DefaultWeightFor(Severity severity) => severity switch
    {
        Severity.Critical => 100,
        Severity.High => 50,
        Severity.Medium => 20,
        Severity.Low => 5,
        _ => 1,
    };

    public void UpdatePattern(string patternOrConfigJson)
    {
        PatternOrConfigJson = patternOrConfigJson;
        Version++;
    }

    public void SetEnabled(bool enabled) => Enabled = enabled;
}
