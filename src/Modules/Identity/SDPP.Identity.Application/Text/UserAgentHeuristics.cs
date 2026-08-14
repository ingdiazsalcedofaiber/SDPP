namespace SDPP.Identity.Application.Text;

/// <summary>Coarse, dependency-free OS sniff from a User-Agent header — good enough for the
/// "sistema_operativo" audit/session column, not meant to be a full UA parser.</summary>
public static class UserAgentHeuristics
{
    public static string? ParseOperatingSystem(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        return userAgent switch
        {
            _ when userAgent.Contains("Windows NT", StringComparison.OrdinalIgnoreCase) => "Windows",
            _ when userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) => "macOS",
            _ when userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) => "Android",
            _ when userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) => "iOS",
            _ when userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase) => "Linux",
            _ => "Desconocido",
        };
    }
}
