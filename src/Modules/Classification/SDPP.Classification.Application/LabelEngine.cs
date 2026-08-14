using SDPP.Classification.Domain.Aggregates;

namespace SDPP.Classification.Application;

public sealed record LabelingResult(IReadOnlyList<string> Labels, string? BusinessCategory);

/// <summary>
/// Aggregates a document's Findings into its Labels (union of every matched rule's tags, e.g.
/// "PII", "MEDICO") and its single BusinessCategory (see docs/05-security/dlp-engine.md §4) — the
/// category is what ProtectionEngine/ClassificationPolicy match against for rules like "Historia
/// Clínica no puede convertirse a DOCX", so ties are broken by picking the highest-weight finding
/// that actually asserts one, not just the first one encountered.
/// </summary>
public interface ILabelEngine
{
    LabelingResult Resolve(IReadOnlyCollection<Finding> findings);
}

public sealed class LabelEngine : ILabelEngine
{
    public LabelingResult Resolve(IReadOnlyCollection<Finding> findings)
    {
        var labels = findings
            .SelectMany(f => f.Labels)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var businessCategory = findings
            .Where(f => f.BusinessCategory is not null)
            .OrderByDescending(f => f.Weight)
            .Select(f => f.BusinessCategory)
            .FirstOrDefault();

        return new LabelingResult(labels, businessCategory);
    }
}
