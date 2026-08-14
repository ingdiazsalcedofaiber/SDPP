namespace SDPP.Identity.Application.Ports;

/// <summary>The one piece of this feature that MUST run only on the backend — see the explicit
/// requirement that the frontend is never trusted alone. Implementations verify signature, issuer,
/// audience (client ID) and expiration; a null return means "reject", callers never need to
/// inspect why (see docs on validation failures logged by the implementation itself).</summary>
public sealed record GoogleIdentity(string Sub, string Email, bool EmailVerified, string Name, string? Picture, string? HostedDomain);

public interface IGoogleTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
