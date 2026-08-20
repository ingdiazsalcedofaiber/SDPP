using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Outbound half: attaches a shared-secret header to every request on a qualifying HttpClient,
/// unconditionally — unlike ForwardAccessTokenHandler (which forwards the CALLING user's own
/// session and does nothing when there isn't one), this covers the case that handler can't: a
/// server-to-server call with no user session behind it at all, e.g. Signature.Api resolving a
/// signer-access-token request from someone with no SDPP account, calling into Documents.Api to
/// fetch/lock the document. Chain both handlers on the same HttpClient — InternalServiceKeyFilter
/// on the receiving side accepts either credential.
/// </summary>
public sealed class InternalServiceKeyHandler(IConfiguration configuration) : DelegatingHandler
{
    private const string HeaderName = "X-SDPP-Internal-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = configuration["Services:InternalApiKey"];
        if (!string.IsNullOrEmpty(key))
        {
            request.Headers.Add(HeaderName, key);
        }
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Inbound half: apply to an endpoint instead of .RequireAuthorization() when it must accept EITHER
/// a normal authenticated SDPP session (the common case — Documents.Api's own internal endpoints
/// today, or an internal recipient who is also logged into SDPP) OR the shared internal-service key
/// (an external recipient's request, relayed by Signature.Api with no user session behind it).
/// Register the endpoint with .AllowAnonymous() so the JwtBearer middleware's normal "require"
/// enforcement doesn't run first — this filter is the actual gate.
/// </summary>
public sealed class InternalServiceKeyFilter(IConfiguration configuration) : IEndpointFilter
{
    private const string HeaderName = "X-SDPP-Internal-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            return await next(context);
        }

        var configuredKey = configuration["Services:InternalApiKey"];
        var providedKey = httpContext.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrEmpty(configuredKey) && !string.IsNullOrEmpty(providedKey) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configuredKey), Encoding.UTF8.GetBytes(providedKey)))
        {
            return await next(context);
        }

        return Results.Unauthorized();
    }
}

/// <summary>
/// Stricter sibling of InternalServiceKeyFilter: requires the shared key unconditionally, with NO
/// "any authenticated session passes" fallback. Use this — never InternalServiceKeyFilter — for an
/// endpoint that is genuinely internal-only (no legitimate direct end-user/browser caller exists at
/// all, e.g. Documents.Api's /lock and /signed-version, Audit.Api's /records/export and
/// /records/integrity). InternalServiceKeyHandler (the outbound half) always attaches the key on
/// every server-to-server call regardless of whether a user session is also being forwarded, so
/// switching a genuinely-internal endpoint to this filter never breaks a legitimate caller — it only
/// closes the gap where "I happen to be logged in as *some* SDPP user" was being accepted as
/// equivalent to "I am the Signature module calling on the platform's behalf", which let any
/// authenticated user reach an endpoint whose group-level role policy was bypassed by the
/// .AllowAnonymous() this filter pattern requires (see the incident this fixed: /records/export
/// leaking the full audit trail to non-Auditor users, and /lock letting any user irreversibly lock
/// another user's document).
/// </summary>
public sealed class InternalServiceKeyOnlyFilter(IConfiguration configuration) : IEndpointFilter
{
    private const string HeaderName = "X-SDPP-Internal-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var configuredKey = configuration["Services:InternalApiKey"];
        var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrEmpty(configuredKey) && !string.IsNullOrEmpty(providedKey) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configuredKey), Encoding.UTF8.GetBytes(providedKey)))
        {
            return await next(context);
        }

        return Results.Unauthorized();
    }
}

public static class InternalServiceKeyExtensions
{
    public static IServiceCollection AddSdppInternalServiceKey(this IServiceCollection services)
    {
        services.AddTransient<InternalServiceKeyHandler>();
        return services;
    }
}
