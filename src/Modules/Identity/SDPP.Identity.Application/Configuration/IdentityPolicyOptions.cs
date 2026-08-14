namespace SDPP.Identity.Application.Configuration;

/// <summary>Bound from the "Identity" configuration section — every value here must be editable
/// via config, never hardcoded (bootstrap admin emails, dev-only domain override, session
/// lifetime).</summary>
public sealed class IdentityPolicyOptions
{
    public string[] BootstrapAdminEmails { get; set; } = [];
    public bool AllowDevDomainOverride { get; set; }
    public int SessionAbsoluteLifetimeHours { get; set; } = 8;
}
