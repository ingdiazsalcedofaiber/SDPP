namespace SDPP.Identity.Application.Ports;

public sealed record GeneratedTotpSecret(string Base32Secret, string ManualEntryKey);

/// <summary>RFC 6238 TOTP generation/validation plus QR rendering — implemented in Infrastructure
/// with Otp.NET + QRCoder, neither of which makes network calls (offline, same trade-off already
/// used to prefer TOTP over SMS OTP for the whole feature).</summary>
public interface ITotpService
{
    GeneratedTotpSecret GenerateSecret();

    /// <summary>Renders the standard <c>otpauth://totp/...</c> URI as a scannable QR, returned as a
    /// <c>data:image/png;base64,...</c> URI — no extra QR library needed on the frontend.</summary>
    string BuildQrCodeDataUri(string accountEmail, string base32Secret);

    /// <summary>Validates with a ±1 step (30s) window to tolerate clock drift between the server
    /// and the user's authenticator app.</summary>
    bool ValidateCode(string base32Secret, string code);
}
