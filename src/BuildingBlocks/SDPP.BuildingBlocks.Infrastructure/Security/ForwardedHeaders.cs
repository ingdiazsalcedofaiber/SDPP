using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Every module's Api is only ever reached directly by the Gateway inside the Docker network — the
/// Gateway is the true edge (browser connects to it directly, see SDPP.Gateway/Program.cs's own
/// comment). Without this middleware, HttpCurrentActor.IpAddress (Context.Connection.RemoteIpAddress)
/// resolves to the GATEWAY CONTAINER's own Docker-bridge IP on every request — not the real client
/// IP YARP already observed and forwards via X-Forwarded-For (YARP's default transforms add this
/// automatically, see SDPP.Gateway's ReverseProxy config, which never disables them). This is what
/// makes every IP address recorded in the Signature module's evidence (ConsentRecord, audit events,
/// the printed certificate) an internal Docker address instead of the real one.
/// </summary>
public static class ForwardedHeadersExtensions
{
    public static IApplicationBuilder UseSdppForwardedHeaders(this IApplicationBuilder app)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        };

        // The Gateway container's IP changes every `docker compose up` recreation, so it can't be
        // pinned as a KnownProxy — clearing both lists trusts X-Forwarded-For from any direct
        // caller. Acceptable here because these backend ports are only reachable from inside the
        // Docker network (or, in this local dev compose, from the host machine) — same trust
        // boundary already assumed by InternalServiceKeyFilter's shared-key model, not a new gap.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        return app.UseForwardedHeaders(options);
    }
}
