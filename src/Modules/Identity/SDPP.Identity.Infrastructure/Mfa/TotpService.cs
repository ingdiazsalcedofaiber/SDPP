using OtpNet;
using QRCoder;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Infrastructure.Mfa;

/// <summary>RFC 6238 TOTP via Otp.NET + QR rendering via QRCoder's <see cref="PngByteQRCode"/>
/// (byte-based, no System.Drawing.Common dependency — required since Identity.Api runs on the
/// Alpine base image, which has no GDI+). Both packages are offline/no network calls.</summary>
public sealed class TotpService : ITotpService
{
    private const int SecretLengthBytes = 20; // 160 bits, the RFC 4226/6238-recommended size
    private const string Issuer = "SDPP";

    public GeneratedTotpSecret GenerateSecret()
    {
        var keyBytes = KeyGeneration.GenerateRandomKey(SecretLengthBytes);
        var base32Secret = Base32Encoding.ToString(keyBytes);
        return new GeneratedTotpSecret(base32Secret, FormatManualEntryKey(base32Secret));
    }

    public string BuildQrCodeDataUri(string accountEmail, string base32Secret)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{accountEmail}");
        var otpAuthUri = $"otpauth://totp/{label}?secret={base32Secret}&issuer={Uri.EscapeDataString(Issuer)}&algorithm=SHA1&digits=6&period=30";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        var pngBytes = new PngByteQRCode(qrCodeData).GetGraphic(10);

        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }

    public bool ValidateCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
    }

    private static string FormatManualEntryKey(string base32Secret) =>
        string.Join(" ", Enumerable.Range(0, (base32Secret.Length + 3) / 4).Select(i => base32Secret.Substring(i * 4, Math.Min(4, base32Secret.Length - i * 4))));
}
