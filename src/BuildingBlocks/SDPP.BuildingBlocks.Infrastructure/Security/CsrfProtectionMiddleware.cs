using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>Marks an endpoint as exempt from CSRF checks — only the login endpoints (which run
/// before any session cookie exists) should ever use this.</summary>
public sealed class SkipCsrfAttribute : Attribute;

public static class CsrfEndpointExtensions
{
    public static TBuilder SkipCsrf<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new SkipCsrfAttribute());
        return builder;
    }
}

/// <summary>
/// Double-submit-cookie CSRF protection: Identity.Api sets a non-HttpOnly <c>sdpp_csrf</c> cookie
/// on login, the frontend reads it and echoes it back as the <c>X-CSRF-Token</c> header on every
/// mutating request; this middleware just checks the two match. Combined with SameSite=Strict on
/// every SDPP cookie, this is what makes cookie-based sessions (HttpOnly, so immune to XSS token
/// theft) safe against cross-site request forgery. Only checked for requests that are actually
/// cookie-authenticated (carry the <c>sdpp_at</c> cookie) — a service-to-service call using a
/// bearer header instead is unaffected.
/// </summary>
public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    public const string CsrfCookieName = "sdpp_csrf";
    public const string CsrfHeaderName = "X-CSRF-Token";

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var exempt = endpoint?.Metadata.GetMetadata<SkipCsrfAttribute>() is not null;

        // Checked against either session cookie, not just the access token — a delayed /refresh
        // call can arrive after the 15-minute sdpp_at cookie has already expired out of the
        // browser's jar while the longer-lived sdpp_rt cookie is still present, and that request
        // must not silently skip CSRF just because one of the two cookies happens to be gone.
        var isCookieAuthenticated = context.Request.Cookies.ContainsKey(CookieJwtBearerExtensions.AccessTokenCookieName)
            || context.Request.Cookies.ContainsKey("sdpp_rt");

        if (exempt || SafeMethods.Contains(context.Request.Method) || !isCookieAuthenticated)
        {
            await next(context);
            return;
        }

        var cookieValue = context.Request.Cookies[CsrfCookieName];
        var headerValue = context.Request.Headers[CsrfHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(cookieValue) || string.IsNullOrEmpty(headerValue) || !string.Equals(cookieValue, headerValue, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { title = "Falta o no coincide el token CSRF.", traceId = context.TraceIdentifier });
            return;
        }

        await next(context);
    }
}

public static class CsrfMiddlewareExtensions
{
    public static IApplicationBuilder UseSdppCsrfProtection(this IApplicationBuilder app) => app.UseMiddleware<CsrfProtectionMiddleware>();
}
