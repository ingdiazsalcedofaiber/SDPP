namespace SDPP.Classification.Infrastructure.Protection;

/// <summary>
/// Bound from protection-policies.json (section "ProtectionPolicies") via IOptionsMonitor with
/// reloadOnChange — every rule the automatic-protection spec calls "configurable" (qué proteger
/// por nivel, plantillas, estilo del watermark) lives here, never hardcoded in ProtectionEngine or
/// PdfProtectionStampingEngine. Keyed by ClassificationLevel.ToString() so a level with no entry
/// falls back to <see cref="ProtectionLevelPolicy"/>'s all-off defaults (equivalent to Público).
/// </summary>
public sealed class ProtectionPolicyConfig
{
    public Dictionary<string, ProtectionLevelPolicy> Levels { get; set; } = new();

    public ProtectionLevelPolicy ResolveFor(string classificationLevel) =>
        Levels.GetValueOrDefault(classificationLevel) ?? new ProtectionLevelPolicy();
}

public sealed class ProtectionLevelPolicy
{
    public bool ApplyWatermark { get; set; }
    public bool ApplyFooter { get; set; }
    public bool ApplyHeader { get; set; }
    public bool EmbedMetadata { get; set; }
    public bool SignIntegrity { get; set; }
    public bool BlockPrintAndCopy { get; set; }
    public bool BlockEditableConversion { get; set; }
    public bool NotifyAdmin { get; set; }

    /// <summary>Placeholders: {classification} {category} {labels} {riskScore} {user} {area}
    /// {date} {ip} {auditId} — rendered by IWatermarkContentBuilder, never fixed text.</summary>
    public string WatermarkTemplate { get; set; } =
        "{classification} · {user} · {date} · Auditoría {auditId}";

    public string FooterTemplate { get; set; } = "SDPP · {classification} · {date} · Auditoría {auditId}";

    public string HeaderTemplate { get; set; } = "SDPP · Documento {classification}";

    public WatermarkStyle Watermark { get; set; } = new();
}

public sealed class WatermarkStyle
{
    public string ColorHex { get; set; } = "#C00000";
    public double Opacity { get; set; } = 0.18;
    public double FontSize { get; set; } = 22;
    public double AngleDegrees { get; set; } = -45;
}
