using SDPP.BuildingBlocks.Domain;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// A single signer invited to a <see cref="SignatureEnvelope"/> — child entity, lifecycle entirely
/// owned by the envelope (same shape as DocumentInstance/ConversionJob). May or may not resolve to
/// an internal SDPP account (<see cref="MatchedUserId"/>) — an unmatched recipient signs through the
/// public magic-link + email-OTP flow (see SignerAccessChallenge), a matched one through their
/// normal SDPP session.
/// </summary>
public sealed class EnvelopeRecipient : Entity<Guid>
{
    public Guid EnvelopeId { get; private set; }

    /// <summary>Only meaningful when the parent envelope's SigningMode is Sequential.</summary>
    public int Order { get; private set; }

    public string Email { get; private set; } = null!;
    public string FullName { get; private set; } = null!;

    /// <summary>Resolved at Send() time against Identity by email — null means this recipient has
    /// no SDPP account and must sign as an external party.</summary>
    public Guid? MatchedUserId { get; private set; }

    /// <summary>Front-desk "sign right now, on this device" mode — the staff member has already
    /// verified the signer's identity in person, so the usual email-OTP round trip (which would
    /// otherwise apply to any MatchedUserId-null recipient) is skipped entirely; the raw access-link
    /// token itself is treated as sufficient (see RecipientAccessAuthorization.CanAct). Email is
    /// still required and still used — just for delivering the completion certificate afterward
    /// (CompleteRecipientSigningHandler), not for identity verification. Defaults to false so every
    /// existing/remote envelope keeps behaving exactly as before.</summary>
    public bool InPerson { get; private set; }

    public RecipientStatus Status { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? ViewedAtUtc { get; private set; }
    public string? ViewedIpAddress { get; private set; }
    public DateTime? SignedAtUtc { get; private set; }
    public string? SignedIpAddress { get; private set; }

    /// <summary>"SdppSession" (matched account, authenticated via their own SDPP session) or
    /// "EmailOtp" (external, authenticated via the emailed one-time code) — see
    /// RecipientAccessAuthorization, the single place that decides which one applied. Printed on
    /// the completion certificate, same purpose as DocuSign's "Nivel de seguridad" line.</summary>
    public string? AuthMethodUsed { get; private set; }

    public DateTime? DeclinedAtUtc { get; private set; }
    public string? DeclineReason { get; private set; }

    /// <summary>Tracks the (Fase I) reminder job's own cadence/cap for this recipient — never
    /// touched by anything else. Null LastReminderSentAtUtc means "count from SentAtUtc instead".</summary>
    public DateTime? LastReminderSentAtUtc { get; private set; }
    public int ReminderCount { get; private set; }

    /// <summary>Explicit "acepto firmar electrónicamente" acceptance — required before this
    /// recipient can be marked Signed (see SignatureEnvelope.RegisterSignature). Distinct from
    /// AuthMethodUsed: this is legal consent to the process, not proof of identity.</summary>
    public DateTime? ConsentAcceptedAtUtc { get; private set; }
    public string? ConsentIpAddress { get; private set; }
    public string? ConsentUserAgent { get; private set; }

    private EnvelopeRecipient() { } // EF Core

    internal static EnvelopeRecipient Create(Guid envelopeId, string email, string fullName, int order, bool inPerson = false)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("El correo del firmante es obligatorio.");
        }

        return new EnvelopeRecipient
        {
            Id = Guid.NewGuid(),
            EnvelopeId = envelopeId,
            Email = email.Trim(),
            FullName = string.IsNullOrWhiteSpace(fullName) ? email.Trim() : fullName.Trim(),
            Order = order,
            Status = RecipientStatus.Pending,
            InPerson = inPerson,
        };
    }

    internal void MarkMatched(Guid? userId) => MatchedUserId = userId;

    internal void MarkSent()
    {
        Status = RecipientStatus.Sent;
        SentAtUtc = DateTime.UtcNow;
    }

    internal void MarkViewed(string? ipAddress)
    {
        if (Status == RecipientStatus.Sent)
        {
            Status = RecipientStatus.Viewed;
            ViewedAtUtc = DateTime.UtcNow;
            ViewedIpAddress = ipAddress;
        }
    }

    internal void MarkConsentAccepted(string? ipAddress, string? userAgent)
    {
        ConsentAcceptedAtUtc = DateTime.UtcNow;
        ConsentIpAddress = ipAddress;
        ConsentUserAgent = userAgent;
    }

    internal void MarkSigned(string? ipAddress, string authMethod)
    {
        Status = RecipientStatus.Signed;
        SignedAtUtc = DateTime.UtcNow;
        SignedIpAddress = ipAddress;
        AuthMethodUsed = authMethod;
    }

    internal void MarkDeclined(string reason)
    {
        Status = RecipientStatus.Declined;
        DeclinedAtUtc = DateTime.UtcNow;
        DeclineReason = reason;
    }

    internal void MarkExpired() => Status = RecipientStatus.Expired;

    internal void MarkReminderSent()
    {
        LastReminderSentAtUtc = DateTime.UtcNow;
        ReminderCount++;
    }
}
