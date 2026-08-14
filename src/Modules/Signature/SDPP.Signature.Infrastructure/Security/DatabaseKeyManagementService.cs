using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Security;

/// <summary>
/// Database-backed IKeyManagementService — lazily generates and holds SDPP's own ECDSA P-256
/// platform signing key on first use, private key encrypted at rest (AES-256-GCM under
/// `Signature:KeyEncryptionKey`, same technique as Identity's AesMfaSecretProtector). Deliberately
/// swappable: nothing outside this class and DependencyInjection ever names it, so a real KMS/HSM-
/// backed IKeyManagementService can replace it later without touching any caller (see
/// IKeyManagementService's doc comment). Not a certification authority and never claims to be one —
/// see DocumentSignature's doc comment for what this signature is and is not.
/// </summary>
public sealed class DatabaseKeyManagementService(ISignatureKeyRepository repository, IConfiguration configuration) : IKeyManagementService
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const string AlgorithmName = "ES256"; // ECDSA P-256 / SHA-256, per JOSE naming (RFC 7518)

    public async Task<CryptographicSignatureResult> SignAsync(byte[] payload, CancellationToken cancellationToken = default)
    {
        var key = await GetOrCreateActiveKeyAsync(cancellationToken);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Decrypt(key.EncryptedPrivateKey), out _);
        var signature = ecdsa.SignData(payload, HashAlgorithmName.SHA256);
        return new CryptographicSignatureResult(key.Id, key.Algorithm, Convert.ToBase64String(signature));
    }

    public async Task<bool> VerifyAsync(byte[] payload, string signatureBase64, Guid keyId, CancellationToken cancellationToken = default)
    {
        var key = await repository.GetByIdAsync(keyId, cancellationToken);
        if (key is null)
        {
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key.PublicKeyBase64), out _);
            return ecdsa.VerifyData(payload, Convert.FromBase64String(signatureBase64), HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }

    private async Task<SignatureKey> GetOrCreateActiveKeyAsync(CancellationToken cancellationToken)
    {
        var existing = await repository.GetActiveAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        var encryptedPrivateKey = Encrypt(ecdsa.ExportPkcs8PrivateKey());

        var key = SignatureKey.Create(AlgorithmName, publicKeyBase64, encryptedPrivateKey);
        repository.Add(key);
        return key;
    }

    private string Encrypt(byte[] plaintext)
    {
        var keyBytes = ResolveEncryptionKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(keyBytes, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintext, cipherBytes, tag);
        }

        var result = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    private byte[] Decrypt(string ciphertextBase64)
    {
        var keyBytes = ResolveEncryptionKey();
        var payload = Convert.FromBase64String(ciphertextBase64);

        var nonce = payload[..NonceSizeBytes];
        var tag = payload[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var cipherBytes = payload[(NonceSizeBytes + TagSizeBytes)..];
        var plaintext = new byte[cipherBytes.Length];

        using (var aesGcm = new AesGcm(keyBytes, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plaintext);
        }

        return plaintext;
    }

    private byte[] ResolveEncryptionKey()
    {
        var configuredKey = configuration["Signature:KeyEncryptionKey"]
            ?? throw new InvalidOperationException("Falta configurar 'Signature:KeyEncryptionKey'.");
        var keyBytes = Encoding.UTF8.GetBytes(configuredKey);

        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException("'Signature:KeyEncryptionKey' debe tener exactamente 32 bytes (AES-256).");
        }

        return keyBytes;
    }
}
