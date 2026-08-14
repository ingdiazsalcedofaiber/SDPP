using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Infrastructure.Mfa;

/// <summary>AES-256-GCM encryption of the TOTP secret at rest, keyed from
/// <c>Identity:MfaEncryptionKey</c> — a static configured key (same shape as
/// <c>Authentication:SigningKey</c> in JwtAccessTokenIssuer), deliberately not ASP.NET Core's Data
/// Protection API (see IMfaSecretProtector doc comment for why). Ciphertext layout is
/// nonce(12) || tag(16) || cipher, all base64-encoded together.</summary>
public sealed class AesMfaSecretProtector(IConfiguration configuration) : IMfaSecretProtector
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public string Encrypt(string plaintextSecret)
    {
        var key = ResolveKey();
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextSecret);
        var cipherBytes = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, cipherBytes, tag);
        }

        var payload = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, payload, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);
        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        var key = ResolveKey();
        var payload = Convert.FromBase64String(ciphertext);

        var nonce = payload[..NonceSizeBytes];
        var tag = payload[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var cipherBytes = payload[(NonceSizeBytes + TagSizeBytes)..];
        var plaintextBytes = new byte[cipherBytes.Length];

        using (var aesGcm = new AesGcm(key, TagSizeBytes))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private byte[] ResolveKey()
    {
        var configuredKey = configuration["Identity:MfaEncryptionKey"]
            ?? throw new InvalidOperationException("Falta configurar 'Identity:MfaEncryptionKey'.");
        var keyBytes = Encoding.UTF8.GetBytes(configuredKey);

        if (keyBytes.Length != 32)
        {
            throw new InvalidOperationException("'Identity:MfaEncryptionKey' debe tener exactamente 32 bytes (AES-256).");
        }

        return keyBytes;
    }
}
