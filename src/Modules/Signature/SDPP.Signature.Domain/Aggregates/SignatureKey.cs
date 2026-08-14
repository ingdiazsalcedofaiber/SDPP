using SDPP.BuildingBlocks.Domain;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// SDPP's own ECDSA P-256 platform key, used to cryptographically attest each recipient's
/// completed signature event (see DocumentSignature) — this is NOT a personal PKI certificate
/// issued to any individual signer, and SDPP is not a certification authority. Modeled as its own
/// aggregate, independent of SignatureEnvelope, since one key signs many envelopes over its
/// lifetime and is rotated/revoked on its own schedule. Only IKeyManagementService ever reads
/// EncryptedPrivateKey; nothing else in the system touches it.
/// </summary>
public sealed class SignatureKey : AggregateRoot<Guid>
{
    public string Algorithm { get; private set; } = null!;
    public string PublicKeyBase64 { get; private set; } = null!;

    /// <summary>AES-256-GCM ciphertext of the PKCS8 private key, base64 — same at-rest encryption
    /// technique as Identity's MfaSecretEncrypted (see AesMfaSecretProtector). Never decrypted
    /// outside DatabaseKeyManagementService, never returned by any query/endpoint.</summary>
    public string EncryptedPrivateKey { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public SignatureKeyStatus Status { get; private set; }

    private SignatureKey() { } // EF Core

    public static SignatureKey Create(string algorithm, string publicKeyBase64, string encryptedPrivateKey)
    {
        return new SignatureKey
        {
            Id = Guid.NewGuid(),
            Algorithm = algorithm,
            PublicKeyBase64 = publicKeyBase64,
            EncryptedPrivateKey = encryptedPrivateKey,
            CreatedAtUtc = DateTime.UtcNow,
            Status = SignatureKeyStatus.Active,
        };
    }

    /// <summary>Revoking a key never invalidates past DocumentSignature rows — those keep
    /// referencing this key's Id/PublicKeyBase64 permanently and stay independently verifiable;
    /// revocation only stops this key from being handed out for new signatures going forward.</summary>
    public void Revoke()
    {
        if (Status == SignatureKeyStatus.Revoked)
        {
            throw new DomainException("Esta clave ya está revocada.");
        }
        Status = SignatureKeyStatus.Revoked;
        RevokedAtUtc = DateTime.UtcNow;
    }
}
