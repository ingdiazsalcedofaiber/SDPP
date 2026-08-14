namespace SDPP.BuildingBlocks.Contracts.Documents;

/// <summary>
/// Published by SDPP.Signature.Api's multi-signer envelope flow — replaces the old single-signer
/// DocumentSignedV1 (see the DocuSign-style module redesign plan). Kept in this folder rather than
/// a Signature-specific one: the convention here is "which domain entity is affected" (still a
/// Document, ultimately), not "which module published it" — see ProtectionEvents.cs for the same
/// pattern from Classification.
/// </summary>
public sealed record SignatureEnvelopeSentV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid SourceDocumentId, string Title,
    string SigningMode, int RecipientCount, DateTime? DueDateUtc, ActorSnapshot Actor) : IIntegrationEvent;

/// <summary>First time a recipient opens their signing link — not raised again on subsequent
/// visits to the same link (see ViewEnvelopeAccessHandler's wasFirstView check).</summary>
public sealed record EnvelopeRecipientViewedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid RecipientId, string RecipientEmail,
    string? IpAddress, string? UserAgent) : IIntegrationEvent;

/// <summary>The richest event in the module — carries every field the spec's audit section asks
/// for: who (email/fullname/MatchedUserId if internal), how they authenticated, which signature
/// method(s) they used, both hashes (FinalSha256Hash is only non-null when this signature also
/// completed the envelope — see CompleteRecipientSigningHandler), and where from.</summary>
public sealed record EnvelopeRecipientSignedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid RecipientId, string RecipientEmail, string RecipientFullName,
    Guid? MatchedUserId, string AuthMethod, string SignatureMethodsUsed,
    string OriginalSha256Hash, string? FinalSha256Hash, string? IpAddress, string? UserAgent) : IIntegrationEvent;

public sealed record EnvelopeRecipientDeclinedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid RecipientId, string RecipientEmail, string Reason,
    string? IpAddress, string? UserAgent) : IIntegrationEvent;

/// <summary>Fired once, when the last recipient's signature triggers final PDF assembly — carries
/// SourceDocumentVersionId so Classification's consumer can inherit the source version's
/// fingerprint/classification onto the final signed version (signing never reclassifies).</summary>
public sealed record SignatureEnvelopeCompletedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid SourceDocumentId, Guid SourceDocumentVersionId,
    Guid FinalDocumentId, Guid FinalDocumentVersionId, string OriginalSha256Hash, string FinalSha256Hash) : IIntegrationEvent;

public sealed record SignatureEnvelopeCancelledV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid CancelledByUserId) : IIntegrationEvent;

/// <summary>Fired once, when a Draft envelope's source document is attached and hashed — the
/// starting point of the evidentiary chain for this envelope (CreateEnvelopeHandler).</summary>
public sealed record SignatureEnvelopeDocumentAttachedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid SourceDocumentId, string OriginalSha256Hash, Guid CreatedByUserId) : IIntegrationEvent;

public sealed record RecipientOtpRequestedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid RecipientId, string RecipientEmail, string? IpAddress, string? UserAgent) : IIntegrationEvent;

/// <summary>Only the successful verification — a failed attempt is a security-relevant event of its
/// own kind, not "the same event that didn't happen"; VerifyOtpHandler doesn't currently publish one
/// for failures, tracked as a gap for a future pass rather than blocking this one.</summary>
public sealed record RecipientOtpValidatedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid RecipientId, string RecipientEmail, string? IpAddress, string? UserAgent) : IIntegrationEvent;

/// <summary>Published by the (Fase I) envelope-lifecycle job when a past-due envelope crosses its
/// DueDateUtc — the contract exists now so Audit's consumer is ready before the publisher lands.</summary>
public sealed record SignatureEnvelopeExpiredV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, Guid SourceDocumentId) : IIntegrationEvent;

public sealed record CertificateGeneratedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, string CertificateHash, string VerificationUrl) : IIntegrationEvent;

/// <summary>Fired on every call to the public /verify/{envelopeId} endpoint — lets the audit trail
/// show who checked an envelope's integrity and when, without the verifier itself needing to expose
/// anything beyond what VerifyEnvelopeQuery already returns publicly.</summary>
public sealed record EnvelopeVerificationPerformedV1(
    Guid EventId, DateTime OccurredAtUtc, Guid EnvelopeId, bool IsIntact, string? IpAddress, string? UserAgent) : IIntegrationEvent;
