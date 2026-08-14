using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Prometheus;
using SDPP.BuildingBlocks.Infrastructure.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "SDPP.Gateway"));

// The Gateway is the single entry point from the corporate intranet (see
// docs/07-operations/kubernetes-architecture.md §2, sdpp-gateway namespace) — TLS terminates
// here with a certificate issued by the internal PKI, never a public CA. Same shared cookie-JWT
// auth every service uses (see SDPP.Documents.Api/Program.cs) — the Gateway validates it too so
// the rate limiter below can partition by real user id instead of just IP.
builder.Services.AddSdppCookieJwtBearer(builder.Configuration);
builder.Services.AddSdppTokenRevocation(builder.Configuration);

// Per-user sliding-window rate limiting (docs/06-api/api-design.md §1). Backed by in-memory
// state here; swap the partition store for Redis (StackExchange.Redis) when running more than
// one Gateway replica so limits are enforced cluster-wide rather than per-pod.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("sub")?.Value ?? "anonymous"
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            // 100/min was tripping during ordinary interactive use — an envelope editor session
            // (placing/dragging/resizing several fields, each a separate PATCH) can legitimately
            // fire dozens of requests in a short burst. 300/min keeps meaningful abuse protection
            // (a scripted attacker needs far more than this) while giving real editing headroom.
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
        });
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

if (builder.Environment.IsDevelopment())
{
    // The production frontend is served from the same origin as the Gateway behind the intranet
    // reverse proxy, so CORS is never needed there. Two dev-only origins talk to the Gateway
    // (:5080) cross-origin: `npm run dev` (Vite on :5173, see frontend/sdpp-web/README.md), and
    // the docker-compose `web` container's own nginx (:5090) — a separate origin from the Gateway
    // in THIS compose topology, unlike the "same origin behind the reverse proxy" production setup.
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevFrontend", policy => policy
            .WithOrigins("http://localhost:5173", "http://localhost:5090")
            .AllowAnyHeader()
            .AllowAnyMethod()
            // Required for the frontend's fetch(..., { credentials: "include" }) to actually send/
            // receive the sdpp_at/sdpp_rt/sdpp_csrf cookies cross-origin (:5173 → :5080 in dev) —
            // without this the browser silently drops the Set-Cookie response headers entirely.
            .AllowCredentials());
    });
}

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();

// Security headers applied uniformly at the edge (OWASP ASVS V14, docs/05-security/compliance-mapping.md).
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

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevFrontend");
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapMetrics();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapReverseProxy();

app.Run();
