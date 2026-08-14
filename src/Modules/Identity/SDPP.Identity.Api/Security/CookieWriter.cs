using System.Security.Cryptography;
using SDPP.BuildingBlocks.Infrastructure.Security;

namespace SDPP.Identity.Api.Security;

/// <summary>
/// One place that sets/clears the 3 session cookies with the correct flags — used by
/// login/refresh/logout so those endpoints can never drift on HttpOnly/Secure/SameSite
/// (see docs on session design). <see cref="CookieJwtBearerExtensions.AccessTokenCookieName"/> is
/// the same constant name the shared JwtBearer cookie-reader (BuildingBlocks.Infrastructure) reads
/// from — defined once, used by both the writer here and the reader there.
/// </summary>
public static class CookieWriter
{
    public const string AccessTokenCookie = CookieJwtBearerExtensions.AccessTokenCookieName;
    public const string RefreshTokenCookie = "sdpp_rt";
    public const string CsrfCookie = CsrfProtectionMiddleware.CsrfCookieName;
    public const string MfaChallengeCookie = "sdpp_mfa_pending";

    public static void IssueSessionCookies(
        HttpContext httpContext, IWebHostEnvironment env, string accessToken, DateTime accessTokenExpiresAtUtc, string rawRefreshToken)
    {
        // Secure=false only when running plain HTTP in Development (the local Gateway has no TLS
        // termination) — browsers silently drop Secure cookies on a non-HTTPS origin, so hardcoding
        // Always here would break every local login. Never relaxed outside Development.
        var secure = !env.IsDevelopment() || httpContext.Request.IsHttps;
        var sessionExpiry = new DateTimeOffset(DateTime.UtcNow.AddHours(8));

        httpContext.Response.Cookies.Append(AccessTokenCookie, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = new DateTimeOffset(accessTokenExpiresAtUtc, TimeSpan.Zero),
        });

        httpContext.Response.Cookies.Append(RefreshTokenCookie, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = sessionExpiry,
        });

        // Not HttpOnly on purpose — the frontend must be able to read this one and echo it back as
        // X-CSRF-Token (see CsrfProtectionMiddleware).
        httpContext.Response.Cookies.Append(CsrfCookie, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = sessionExpiry,
        });
    }

    public static void ClearSessionCookies(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(AccessTokenCookie, new CookieOptions { Path = "/" });
        httpContext.Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions { Path = "/api/v1/auth" });
        httpContext.Response.Cookies.Delete(CsrfCookie, new CookieOptions { Path = "/" });
    }

    /// <summary>Issued right after Google validates a login but before MFA is proven — opaque,
    /// scoped to the auth routes only, short-lived (matches MfaChallenge's own 10-minute lifetime).</summary>
    public static void IssueMfaChallengeCookie(HttpContext httpContext, IWebHostEnvironment env, string rawChallengeToken, DateTime expiresAtUtc)
    {
        var secure = !env.IsDevelopment() || httpContext.Request.IsHttps;

        httpContext.Response.Cookies.Append(MfaChallengeCookie, rawChallengeToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
        });
    }

    public static void ClearMfaChallengeCookie(HttpContext httpContext) =>
        httpContext.Response.Cookies.Delete(MfaChallengeCookie, new CookieOptions { Path = "/api/v1/auth" });
}
