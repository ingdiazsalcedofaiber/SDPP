using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Infrastructure.Google;

/// <summary>
/// Validates a Google ID token server-side using Google's own maintained library — it fetches and
/// caches Google's signing keys (https://www.googleapis.com/oauth2/v3/certs) and rotates them
/// internally, so this class never has to. Verifies signature, issuer, audience (our OAuth Client
/// ID) and expiration; a failed/expired/wrong-audience token returns null rather than throwing, so
/// callers can map it to a clean "invalid token" outcome instead of an unhandled exception.
/// </summary>
public sealed class GoogleJsonWebSignatureTokenValidator(IConfiguration configuration, ILogger<GoogleJsonWebSignatureTokenValidator> logger)
    : IGoogleTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = configuration["Identity:GoogleClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            logger.LogError("Identity:GoogleClientId no está configurado — no se puede validar ningún token de Google.");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
            });

            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name, payload.Picture, payload.HostedDomain);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Token de Google inválido o expirado.");
            return null;
        }
    }
}
