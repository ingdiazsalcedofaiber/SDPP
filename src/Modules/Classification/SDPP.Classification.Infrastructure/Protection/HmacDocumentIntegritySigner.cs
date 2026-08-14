using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SDPP.Classification.Infrastructure.Protection;

/// <summary>
/// Internal tamper-evidence, not a legally-binding signature. SHA-256 the protected bytes, then
/// HMAC-SHA256 that hash with a platform-wide secret (never derived from the file itself, so an
/// attacker who can edit the file can't also forge a matching signature) — same "secret via env
/// var, never hardcoded" pattern as Documents.Infrastructure's MinIO KMS key.
/// </summary>
public interface IDocumentIntegritySigner
{
    string Sign(byte[] fileBytes);

    bool Verify(byte[] fileBytes, string signature);
}

public sealed class HmacDocumentIntegritySigner : IDocumentIntegritySigner
{
    private readonly byte[] _secretKey;

    public HmacDocumentIntegritySigner(IConfiguration configuration)
    {
        var secret = configuration["Protection:HmacSecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "Falta configurar 'Protection:HmacSecretKey' — requerido para firmar la integridad de documentos protegidos.");
        }
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    public string Sign(byte[] fileBytes)
    {
        var contentHash = SHA256.HashData(fileBytes);
        var signature = HMACSHA256.HashData(_secretKey, contentHash);
        return Convert.ToHexString(signature);
    }

    public bool Verify(byte[] fileBytes, string signature) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Sign(fileBytes)), Convert.FromHexString(signature));
}
