using SDPP.BuildingBlocks.Domain;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// SDPP's cryptographic attestation of one recipient's completed signing turn — created once per
/// recipient, at the moment SignatureEnvelope.RegisterSignature marks them Signed (see
/// SignatureEnvelope.RecordCryptographicSignature). This is a PLATFORM attestation: SDPP signs the
/// canonical evidence record with its own key (see SignatureKey/IKeyManagementService), which
/// protects that record's integrity — it is NOT a personal digital signature issued to the
/// recipient under their own PKI certificate, and SDPP is not a certification authority. Anyone
/// holding the public key can independently verify that this exact recorded signature event (who,
/// which document/version, what evidence, when) has not been altered since SDPP recorded it.
/// </summary>
public sealed class DocumentSignature : Entity<Guid>
{
    public Guid EnvelopeId { get; private set; }
    public Guid RecipientId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }

    /// <summary>The source document's SHA-256 at the moment this recipient signed — not the final
    /// assembled multi-signer document's hash, which doesn't exist yet until every recipient has
    /// signed and CompleteWithFinalDocument runs.</summary>
    public string DocumentHashAtSigning { get; private set; } = null!;

    /// <summary>SHA-256 over this recipient's own field SignatureHash values plus identity/consent/
    /// timestamp context — the exact bytes SDPP's platform key signs (CryptographicSignatureBase64).</summary>
    public string CanonicalPayloadHash { get; private set; } = null!;

    public string CryptographicSignatureBase64 { get; private set; } = null!;
    public Guid PublicKeyId { get; private set; }
    public string Algorithm { get; private set; } = null!;

    /// <summary>Links to the recipient's ConsentRecord — null until that entity exists (see the
    /// module's later ConsentRecord phase); recipient.ConsentAcceptedAtUtc remains the source of
    /// truth for consent until then.</summary>
    public Guid? ConsentId { get; private set; }

    public DateTime TimestampUtc { get; private set; }

    /// <summary>"SERVER_TIMESTAMP" today — SDPP's own clock, not a certified RFC 3161 timestamping
    /// authority. Never printed or claimed as a trusted timestamp (see ITimestampAuthorityService).</summary>
    public string TimestampSource { get; private set; } = null!;

    private DocumentSignature() { } // EF Core

    internal static DocumentSignature Create(
        Guid envelopeId, Guid recipientId, Guid documentId, Guid documentVersionId, string documentHashAtSigning,
        string canonicalPayloadHash, string cryptographicSignatureBase64, Guid publicKeyId, string algorithm,
        Guid? consentId, DateTime timestampUtc, string timestampSource)
    {
        return new DocumentSignature
        {
            Id = Guid.NewGuid(),
            EnvelopeId = envelopeId,
            RecipientId = recipientId,
            DocumentId = documentId,
            DocumentVersionId = documentVersionId,
            DocumentHashAtSigning = documentHashAtSigning,
            CanonicalPayloadHash = canonicalPayloadHash,
            CryptographicSignatureBase64 = cryptographicSignatureBase64,
            PublicKeyId = publicKeyId,
            Algorithm = algorithm,
            ConsentId = consentId,
            TimestampUtc = timestampUtc,
            TimestampSource = timestampSource,
        };
    }
}
