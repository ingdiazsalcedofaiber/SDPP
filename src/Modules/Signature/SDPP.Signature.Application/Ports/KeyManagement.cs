namespace SDPP.Signature.Application.Ports;

public sealed record CryptographicSignatureResult(Guid KeyId, string Algorithm, string SignatureBase64);

/// <summary>
/// Signs canonical evidence payloads with SDPP's own platform key (ECDSA P-256 today) — an
/// attestation by the platform that protects the recorded evidence from tampering, never a
/// personal PKI signature issued to an individual signer (see DocumentSignature's doc comment).
/// Deliberately abstracted behind this port so a real KMS/HSM-backed implementation can replace
/// DatabaseKeyManagementService later without any caller changing.
/// </summary>
public interface IKeyManagementService
{
    Task<CryptographicSignatureResult> SignAsync(byte[] payload, CancellationToken cancellationToken = default);

    /// <summary>Independently verifies a signature against the public key it claims to be signed
    /// with. Returns false — never throws — for any mismatch: wrong key, tampered payload, tampered
    /// signature, unknown/revoked key. Used by the attack-simulation tests and, later, the public
    /// verifier.</summary>
    Task<bool> VerifyAsync(byte[] payload, string signatureBase64, Guid keyId, CancellationToken cancellationToken = default);
}
