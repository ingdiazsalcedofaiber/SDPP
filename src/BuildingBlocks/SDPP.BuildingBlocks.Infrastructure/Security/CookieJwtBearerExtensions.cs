using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SDPP.BuildingBlocks.Application;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// One shared JwtBearer registration for every service (Documents/Classification/Audit/Identity
/// Apis + Gateway) — replaces the duplicated `if(Development){DevAuth}else{AddJwtBearer}` block
/// that used to live, byte-for-byte identical, in 3 separate Program.cs files. All 5 services
/// trust the same symmetric signing key (Authentication:SigningKey) issued by Identity.Api — see
/// the "no Authority/JWKS discovery" design decision, they're deployed together, not external
/// relying parties. Reads the token from the <c>sdpp_at</c> HttpOnly cookie first (falls back to
/// the <c>Authorization</c> header for service-to-service calls, e.g.
/// Classification→Documents `/extracted-text`), and checks the shared Redis revocation blocklist
/// on every request via <see cref="IRevokedTokenStore"/>.
/// </summary>
public static class CookieJwtBearerExtensions
{
    public const string AccessTokenCookieName = "sdpp_at";

    public static IServiceCollection AddSdppCookieJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = configuration["Authentication:SigningKey"]
            ?? throw new InvalidOperationException("Falta configurar 'Authentication:SigningKey'.");
        var issuer = configuration["Authentication:Issuer"] ?? "sdpp-identity";
        var audience = configuration["Authentication:Audience"] ?? "sdpp-platform";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Preserves claim types exactly as issued ("sub", "name", ...) — without this,
                // ASP.NET Core silently remaps several standard JWT claim names (sub, name, email)
                // to legacy XML claim-type URIs, and HttpCurrentActor's literal FindFirstValue("sub")/
                // ("name") lookups would stop matching.
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = false; // TLS is terminated in front of the Gateway in production, not per-service here
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(AccessTokenCookieName, out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
                        {
                            context.Token = cookieToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirst("jti")?.Value;
                        var sub = context.Principal?.FindFirst("sub")?.Value;
                        var issuedAtClaim = context.Principal?.FindFirst("iat")?.Value;

                        if (jti is null || sub is null || !Guid.TryParse(sub, out var userId))
                        {
                            context.Fail("El token no contiene los claims requeridos.");
                            return;
                        }

                        var issuedAtUtc = issuedAtClaim is not null && long.TryParse(issuedAtClaim, out var unixSeconds)
                            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime
                            : DateTime.UtcNow;

                        var revokedTokenStore = context.HttpContext.RequestServices.GetRequiredService<IRevokedTokenStore>();
                        if (await revokedTokenStore.IsRevokedAsync(jti, userId, issuedAtUtc, context.HttpContext.RequestAborted))
                        {
                            context.Fail("La sesión fue cerrada o revocada.");
                        }
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }
}
