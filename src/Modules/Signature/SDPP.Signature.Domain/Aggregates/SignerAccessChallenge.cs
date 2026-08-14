using SDPP.BuildingBlocks.Domain;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// The access artifact behind a recipient's secure signing link — modeled structurally on
/// Identity's MfaChallenge (opaque hashed token, expiry, consumed-once, failed-attempt lockout) but
/// keyed by <see cref="RecipientId"/> instead of a platform UserId, since an external recipient has
/// no SDPP account. Two independent sub-states live on the same row: the link itself (always
/// required, both for internal and external recipients — proof of "something they have") and the
/// OTP (only required for recipients with no MatchedUserId — proof of "something only they can
/// receive", their email inbox). A recipient's link stays usable for the lifetime of the envelope's
/// due date, unlike a one-shot MfaChallenge — reopening the signing page to review before signing is
/// expected UX, not a replay.
/// </summary>
public sealed class SignerAccessChallenge : AggregateRoot<Guid>
{
    public const int MaxOtpAttempts = 5;

    public Guid RecipientId { get; private set; }

    /// <summary>SHA-256 hex of the opaque token embedded in the recipient's emailed link.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LinkExpiresAtUtc { get; private set; }

    public string? OtpCodeHash { get; private set; }
    public DateTime? OtpExpiresAtUtc { get; private set; }
    public DateTime? OtpConsumedAtUtc { get; private set; }
    public int OtpFailedAttempts { get; private set; }

    /// <summary>Third sub-state on the same row — issued once the OTP is verified (external
    /// recipients only), presented as a bearer credential on every subsequent field-fill/complete/
    /// decline call so that knowing the emailed link alone is never enough to act; proof of having
    /// also received the OTP email is required first.</summary>
    public string? SessionTokenHash { get; private set; }
    public DateTime? SessionTokenExpiresAtUtc { get; private set; }

    public bool IsLinkUsable => DateTime.UtcNow < LinkExpiresAtUtc;
    public bool IsOtpUsable =>
        OtpCodeHash is not null && OtpConsumedAtUtc is null &&
        OtpExpiresAtUtc is not null && DateTime.UtcNow < OtpExpiresAtUtc && OtpFailedAttempts < MaxOtpAttempts;
    public bool IsSessionUsable => SessionTokenHash is not null && SessionTokenExpiresAtUtc is not null && DateTime.UtcNow < SessionTokenExpiresAtUtc;

    private SignerAccessChallenge() { } // EF Core

    public static SignerAccessChallenge Issue(Guid recipientId, string tokenHash, TimeSpan linkLifetime)
    {
        var now = DateTime.UtcNow;
        return new SignerAccessChallenge
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            LinkExpiresAtUtc = now + linkLifetime,
        };
    }

    /// <summary>Issues a fresh OTP, overwriting any previous unconsumed one — a re-request always
    /// invalidates the prior code rather than stacking valid codes.</summary>
    public void IssueOtp(string otpCodeHash, TimeSpan otpLifetime)
    {
        OtpCodeHash = otpCodeHash;
        OtpExpiresAtUtc = DateTime.UtcNow + otpLifetime;
        OtpConsumedAtUtc = null;
        OtpFailedAttempts = 0;
    }

    public void RegisterOtpFailedAttempt() => OtpFailedAttempts++;

    public void ConsumeOtp() => OtpConsumedAtUtc ??= DateTime.UtcNow;

    public bool ValidateSessionToken(string sessionTokenHash) => IsSessionUsable && SessionTokenHash == sessionTokenHash;

    public void IssueSession(string sessionTokenHash, TimeSpan lifetime)
    {
        SessionTokenHash = sessionTokenHash;
        SessionTokenExpiresAtUtc = DateTime.UtcNow + lifetime;
    }
}
