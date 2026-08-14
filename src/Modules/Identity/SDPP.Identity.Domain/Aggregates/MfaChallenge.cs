using SDPP.BuildingBlocks.Domain;

namespace SDPP.Identity.Domain.Aggregates;

public enum MfaChallengePurpose
{
    Enrollment,
    Login,
}

/// <summary>
/// A short-lived, single-purpose opaque token issued right after Google validates a login but
/// before a real session exists — same "hash at rest, raw value only in the cookie" shape as
/// <see cref="Session.RefreshTokenHash"/>. Deliberately not a JWT: the shared JwtBearer pipeline
/// (AddSdppCookieJwtBearer) only checks signature/issuer/audience/expiry, not purpose, so a JWT
/// here would be usable as a real access token everywhere else. An opaque, DB-resolved token that
/// only the /mfa/* endpoints know how to consume cannot be misused that way.
/// </summary>
public sealed class MfaChallenge : AggregateRoot<Guid>
{
    public const int MaxAttempts = 5;

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hex of the opaque token stored in the <c>sdpp_mfa_pending</c> cookie.</summary>
    public string TokenHash { get; private set; } = null!;

    public MfaChallengePurpose Purpose { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public int FailedAttempts { get; private set; }

    public bool IsUsable => ConsumedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc && FailedAttempts < MaxAttempts;

    private MfaChallenge() { } // EF Core

    public static MfaChallenge Issue(Guid userId, string tokenHash, MfaChallengePurpose purpose, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        return new MfaChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            Purpose = purpose,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + lifetime,
        };
    }

    public void RegisterFailedAttempt() => FailedAttempts++;

    public void Consume() => ConsumedAtUtc ??= DateTime.UtcNow;
}
