using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Infrastructure.Protection;

/// <summary>
/// Renders a ProtectionLevelPolicy template (watermark/footer/header text) against a real
/// ProtectionContext — the automatic-protection spec is explicit that this text must never be
/// fixed/static, always reflecting who, when, and under what audit ID the file was protected.
/// </summary>
public interface IWatermarkContentBuilder
{
    string Build(string template, ProtectionContext context);
}

public sealed class WatermarkContentBuilder : IWatermarkContentBuilder
{
    public string Build(string template, ProtectionContext context) => template
        .Replace("{classification}", context.Classification.ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("{category}", context.Category ?? "N/A", StringComparison.OrdinalIgnoreCase)
        .Replace("{labels}", context.Labels.Count > 0 ? string.Join(", ", context.Labels) : "N/A", StringComparison.OrdinalIgnoreCase)
        .Replace("{riskScore}", context.RiskScore.ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("{user}", context.Actor.FullName ?? context.Actor.Email ?? context.Actor.UserId.ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("{area}", context.Area ?? "N/A", StringComparison.OrdinalIgnoreCase)
        .Replace("{date}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC", StringComparison.OrdinalIgnoreCase)
        .Replace("{ip}", context.Actor.IpAddress ?? "N/A", StringComparison.OrdinalIgnoreCase)
        .Replace("{auditId}", context.AuditId.ToString(), StringComparison.OrdinalIgnoreCase);
}
