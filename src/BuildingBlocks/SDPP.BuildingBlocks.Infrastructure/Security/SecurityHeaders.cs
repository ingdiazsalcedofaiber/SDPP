using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Baseline response security headers (OWASP ASVS V14, docs/05-security/compliance-mapping.md) —
/// originally only applied at the Gateway edge, now shared so every backend API sets them too.
/// Defense in depth: the Gateway is the intended single entry point from the intranet, but a
/// module whose port is reachable directly (misconfigured firewall rule, local debugging, a future
/// deployment that isn't perfectly locked down yet) should still refuse to answer without these.
/// HSTS is skipped in Development — it has no meaning over plain HTTP and would just be noise.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSdppSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("Referrer-Policy", "no-referrer");
            context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
            if (!app.Environment.IsDevelopment())
            {
                context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            }
            await next();
        });

        return app;
    }
}
