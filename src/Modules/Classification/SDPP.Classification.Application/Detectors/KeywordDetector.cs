using System.Text.Json;
using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Detectors;

/// <summary>Config shape: {"keywords": ["contrato", "nda", "las partes acuerdan"]}.</summary>
public sealed class KeywordDetector : IDetector
{
    public DetectorType SupportedType => DetectorType.Keyword;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<Finding> Detect(DetectionContext context, DlpRule rule)
    {
        var config = JsonSerializer.Deserialize<KeywordRuleConfig>(rule.PatternOrConfigJson, JsonOptions)
            ?? throw new InvalidOperationException($"Configuración inválida para la regla '{rule.Name}'.");

        var matchCount = config.Keywords.Count(keyword =>
            context.ExtractedText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return matchCount == 0
            ? []
            : [Finding.Create(
                rule.Name, rule.Category, rule.DefaultSeverity, matchCount, "contenido", rule.Version.ToString(),
                rule.Weight, rule.Labels, rule.BusinessCategory)];
    }

    private sealed record KeywordRuleConfig(List<string> Keywords);
}
