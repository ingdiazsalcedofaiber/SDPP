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
    // Google.Apis.Auth fetches https://www.googleapis.com/oauth2/v3/certs on cache miss with no
    // timeout of its own — on a restricted-egress intranet host that call can hang well past the
    // caller's patience, leaving the login button spinning forever instead of failing visibly.
    // Bounding it here turns "hangs indefinitely" into "fails fast", which is all this deployment
    // needs (see docs/00-overview.md §3 on this running inside a corporate intranet).
    private static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(10);

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var clientId = configuration["Identity:GoogleClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            logger.LogError("Identity:GoogleClientId no está configurado — no se puede validar ningún token de Google.");
            return null;
        }

        using var timeoutCts = new CancellationTokenSource(ValidationTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
            }).WaitAsync(linkedCts.Token);

            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name, payload.Picture, payload.HostedDomain);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Token de Google inválido o expirado.");
            return null;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogError("Verificación del token de Google agotó el tiempo de espera ({Timeout}s) — revisa la salida a internet del servidor hacia googleapis.com.", ValidationTimeout.TotalSeconds);
            return null;
        }
    }
}
