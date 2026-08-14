using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Forwards the current request's access token as an <c>Authorization: Bearer</c> header on
/// outbound service-to-service HttpClient calls (Documents→Classification for inspections/policy
/// evaluation, Classification→Documents for extracted text). The receiving side already falls
/// back to this header when no cookie is present (see CookieJwtBearerExtensions'
/// OnMessageReceived) — that fallback existed but nothing ever populated it, so every internal
/// call arrived unauthenticated and 401'd. This handler is the missing outbound half.
///
/// The token can come from either of two places depending on how deep in the call chain we are:
/// the <c>sdpp_at</c> cookie for a request that originated straight from the browser (the common
/// case), or the current request's own inbound <c>Authorization</c> header when this service is
/// itself mid-hop in a nested call (e.g. Classification received the token via header from
/// Documents, and now needs to forward that same token onward to call Documents back for
/// extracted text — there is no cookie at all on that second hop, only the header already on the
/// inbound request).
/// </summary>
public sealed class ForwardAccessTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var inboundRequest = httpContextAccessor.HttpContext?.Request;
        var token = inboundRequest?.Cookies[CookieJwtBearerExtensions.AccessTokenCookieName];

        if (string.IsNullOrEmpty(token) && inboundRequest?.Headers.Authorization is { Count: > 0 } authHeader)
        {
            var raw = authHeader.ToString();
            const string bearerPrefix = "Bearer ";
            if (raw.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                token = raw[bearerPrefix.Length..];
            }
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

public static class ForwardAccessTokenHandlerExtensions
{
    /// <summary>Registers the handler and its IHttpContextAccessor dependency — call once per
    /// service, then chain <c>.AddHttpMessageHandler&lt;ForwardAccessTokenHandler&gt;()</c> onto
    /// any AddHttpClient registration that calls another SDPP service on behalf of the current
    /// user's request.</summary>
    public static IServiceCollection AddSdppAccessTokenForwarding(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<ForwardAccessTokenHandler>();
        return services;
    }
}
