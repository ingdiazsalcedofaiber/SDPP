using SDPP.BuildingBlocks.Domain;

namespace SDPP.Identity.Domain.Aggregates;

/// <summary>
/// A refresh/session record — the access JWT itself is never persisted (it's stateless and
/// short-lived), but this row is what makes real server-side logout/revocation possible: deleting
/// a user's access means revoking their Session(s), which the shared token-validation pipeline
/// checks via the Redis blocklist keyed off this Id (see IRevokedTokenStore in BuildingBlocks).
/// </summary>
public sealed class Session : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hex of the opaque refresh token — the raw value is never persisted
    /// anywhere (see the "no tokens en texto plano" requirement).</summary>
    public string RefreshTokenHash { get; private set; } = null!;

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? OperatingSystem { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime LastUsedAtUtc { get; private set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    private Session() { } // EF Core

    public static Session Issue(
        Guid userId, string refreshTokenHash, string? ipAddress, string? userAgent, string? operatingSystem, TimeSpan absoluteLifetime)
    {
        var now = DateTime.UtcNow;
        return new Session
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RefreshTokenHash = refreshTokenHash,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OperatingSystem = operatingSystem,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + absoluteLifetime,
            LastUsedAtUtc = now,
        };
    }

    /// <summary>Rotates the refresh secret on every <c>/refresh</c> call (mitigates replay of a
    /// stolen refresh cookie) without extending <see cref="ExpiresAtUtc"/> — the session's
    /// lifetime is absolute, not sliding, so it always dies 8h after login regardless of activity.</summary>
    public void Rotate(string newRefreshTokenHash)
    {
        if (!IsActive)
        {
            throw new DomainException("La sesión ya no está activa.");
        }
        RefreshTokenHash = newRefreshTokenHash;
        LastUsedAtUtc = DateTime.UtcNow;
    }

    public void Revoke() => RevokedAtUtc ??= DateTime.UtcNow;
}
