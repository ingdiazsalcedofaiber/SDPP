using System.Text.Json;
using System.Text.RegularExpressions;
using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Detectors;

/// <summary>
/// Config shape: {"algorithm": "Luhn", "candidateRegex": "..."}. Used for PII.CreditCard (see
/// docs/05-security/dlp-engine.md §3) — a regex alone over-matches any 13-19 digit sequence, so
/// every candidate is additionally validated with the Luhn checksum before counting as a Finding.
/// </summary>
public sealed class ChecksumDetector : IDetector
{
    public DetectorType SupportedType => DetectorType.Checksum;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<Finding> Detect(DetectionContext context, DlpRule rule)
    {
        var config = JsonSerializer.Deserialize<ChecksumRuleConfig>(rule.PatternOrConfigJson, JsonOptions)
            ?? throw new InvalidOperationException($"Configuración inválida para la regla '{rule.Name}'.");

        var candidates = Regex.Matches(context.ExtractedText, config.CandidateRegex, RegexOptions.Compiled, TimeSpan.FromSeconds(2));

        var validCount = candidates
            .Select(m => new string(m.Value.Where(char.IsDigit).ToArray()))
            .Count(digits => digits.Length >= 13 && PassesLuhn(digits));

        return validCount == 0
            ? []
            : [Finding.Create(
                rule.Name, rule.Category, rule.DefaultSeverity, validCount, "contenido", rule.Version.ToString(),
                rule.Weight, rule.Labels, rule.BusinessCategory)];
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private sealed record ChecksumRuleConfig(string Algorithm, string CandidateRegex);
}
