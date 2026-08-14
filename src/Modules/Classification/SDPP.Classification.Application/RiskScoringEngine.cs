using SDPP.Classification.Domain.Aggregates;

namespace SDPP.Classification.Application;

/// <summary>
/// Aggregates a document's Findings into a single numeric RiskScore (see
/// docs/05-security/dlp-engine.md §4) — a distinct, independently testable pipeline stage from
/// classification-by-severity (InspectionResult.Complete), so the two can evolve separately (a
/// score formula changing should never risk breaking the classification-level mapping).
/// </summary>
public interface IRiskScoringEngine
{
    int Score(IReadOnlyCollection<Finding> findings);
}

public sealed class RiskScoringEngine : IRiskScoringEngine
{
    public int Score(IReadOnlyCollection<Finding> findings) =>
        findings.Sum(f => f.Weight * f.MatchCount);
}
