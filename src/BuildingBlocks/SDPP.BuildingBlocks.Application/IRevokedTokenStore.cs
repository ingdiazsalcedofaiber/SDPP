namespace SDPP.BuildingBlocks.Application;

/// <summary>
/// Real, server-side session/token revocation — what makes logout and admin deactivation take
/// effect immediately instead of waiting for a 15-minute access-token TTL to expire on its own.
/// Backed by Redis (see SDPP.BuildingBlocks.Infrastructure.Security.RedisRevokedTokenStore),
/// consulted by every service's shared JwtBearer setup (AddSdppCookieJwtBearer) on every request.
/// Fails open on a Redis outage — platform availability wins over instant revocation; documented
/// trade-off, not a silent gap.
/// </summary>
public interface IRevokedTokenStore
{
    /// <summary>Blocklists a single access token by its <c>jti</c> claim — used by logout.</summary>
    Task RevokeTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Blocklists every access token issued to a user before now — used when an admin
    /// deactivates a user or strips their roles, so already-issued tokens die immediately
    /// regardless of their individual <c>jti</c>.</summary>
    Task RevokeUserAsync(Guid userId, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string jti, Guid userId, DateTime tokenIssuedAtUtc, CancellationToken cancellationToken = default);
}
