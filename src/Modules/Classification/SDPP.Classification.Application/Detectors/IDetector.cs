using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Detectors;

public sealed record DetectionContext(string FileName, string ExtractedText, IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// Strategy implemented once per <see cref="DetectorType"/> (Regex, Keyword, Checksum...), each
/// capable of evaluating any <see cref="DlpRule"/> of that type using its
/// <see cref="DlpRule.PatternOrConfigJson"/> — see docs/05-security/classification-engine.md §4
/// and docs/05-security/dlp-engine.md §3-4. New detection techniques are added by implementing
/// this interface, never by branching inside a use case handler.
/// </summary>
public interface IDetector
{
    DetectorType SupportedType { get; }
    IReadOnlyList<Finding> Detect(DetectionContext context, DlpRule rule);
}
