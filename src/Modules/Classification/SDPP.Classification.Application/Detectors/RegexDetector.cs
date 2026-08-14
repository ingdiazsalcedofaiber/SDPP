using System.Text.Json;
using System.Text.RegularExpressions;
using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Detectors;

/// <summary>Config shape: {"pattern": "...", "searchFileName": true}.</summary>
public sealed class RegexDetector : IDetector
{
    public DetectorType SupportedType => DetectorType.Regex;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<Finding> Detect(DetectionContext context, DlpRule rule)
    {
        var config = JsonSerializer.Deserialize<RegexRuleConfig>(rule.PatternOrConfigJson, JsonOptions)
            ?? throw new InvalidOperationException($"Configuración inválida para la regla '{rule.Name}'.");

        var regex = new Regex(config.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

        var findings = new List<Finding>();

        var contentMatches = regex.Matches(context.ExtractedText);
        if (contentMatches.Count > 0)
        {
            findings.Add(Finding.Create(
                rule.Name, rule.Category, rule.DefaultSeverity, contentMatches.Count, "contenido", rule.Version.ToString(),
                rule.Weight, rule.Labels, rule.BusinessCategory));
        }

        if (config.SearchFileName && regex.IsMatch(context.FileName))
        {
            findings.Add(Finding.Create(
                rule.Name, rule.Category, rule.DefaultSeverity, 1, "nombre del archivo", rule.Version.ToString(),
                rule.Weight, rule.Labels, rule.BusinessCategory));
        }

        return findings;
    }

    private sealed record RegexRuleConfig(string Pattern, bool SearchFileName = false);
}
