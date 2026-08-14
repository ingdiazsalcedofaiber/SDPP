namespace SDPP.Identity.Application.Ports;

/// <summary>Encrypts the TOTP secret at rest with a configured symmetric key (AES-GCM,
/// `Identity:MfaEncryptionKey`) — deliberately not ASP.NET Core's Data Protection API, whose key
/// ring isn't persisted across container restarts in this deployment today; using it here would
/// silently invalidate every enrolled user's MFA on every redeploy. Same "static configured key"
/// shape as <see cref="IAccessTokenIssuer"/>'s <c>Authentication:SigningKey</c>.</summary>
public interface IMfaSecretProtector
{
    string Encrypt(string plaintextSecret);
    string Decrypt(string ciphertext);
}
