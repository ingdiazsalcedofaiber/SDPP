using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using SDPP.BuildingBlocks.Domain;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.BuildingBlocks.Infrastructure.Health;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Documents.Api.Endpoints;
using SDPP.Documents.Application;
using SDPP.Documents.Infrastructure;
using SDPP.Documents.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "SDPP.Documents.Api"));

// Kestrel's own default (~28.6 MB) is well below real-world document sizes (scanned contracts,
// multi-page PDFs) — this is the only module that accepts file uploads, so it's the only one that
// needs raising. 200 MB is a deliberate enterprise-document ceiling, not "unlimited": still bounded
// against abuse, just not surprising for legitimate use. The Gateway/YARP hop in front of this must
// allow at least this much too (see Gateway's own Kestrel config).
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 200 * 1024 * 1024);

// --- Authentication: self-issued JWTs from SDPP.Identity.Api after a Google login, read from the
// sdpp_at HttpOnly cookie — see SDPP.BuildingBlocks.Infrastructure.Security.CookieJwtBearerExtensions.
// Every service (including this one, in every environment) shares this exact setup. ---
builder.Services.AddSdppCookieJwtBearer(builder.Configuration);
builder.Services.AddSdppTokenRevocation(builder.Configuration);
builder.Services.AddSdppCurrentActor();

// "Administrador" as a literal string, not a shared enum/const with Identity.Domain — role names
// cross service boundaries only as a claim value in the shared JWT (published-language contract),
// the same way SDPP.Classification.Domain.ClassificationLevel deliberately duplicates rather than
// references Documents.Domain's copy. Only used by the admin-scope reporting endpoint.
builder.Services.AddAuthorizationBuilder().AddPolicy("Administrador", policy => policy.RequireRole("Administrador"));

builder.Services.AddDocumentsApplication();
builder.Services.AddDocumentsInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddSdppDatabaseHealthCheck<DocumentsDbContext>()
    .AddCheck<SDPP.Documents.Infrastructure.Storage.MinioHealthCheck>("storage");

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Forces every DateTime/DateTime? in every JSON response to serialize with a "Z" (UTC) suffix —
    // see UtcDateTimeJsonConverter's doc comment for the exact bug this fixes.
    options.SerializerOptions.Converters.Add(new SDPP.BuildingBlocks.Infrastructure.Serialization.UtcDateTimeJsonConverter());
    options.SerializerOptions.Converters.Add(new SDPP.BuildingBlocks.Infrastructure.Serialization.UtcNullableDateTimeJsonConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Outbox publisher — a recurring Hangfire job, matching the architecture described in
// docs/01-architecture/c4-diagrams.md (Component diagram, Outbox Publisher).
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DocumentsDb"), new SqlServerStorageOptions
    {
        SchemaName = "documents_hangfire",
    }));
builder.Services.AddHangfireServer();

var app = builder.Build();

// Must run before anything that reads the client IP (logging, ICurrentActor) — see
// UseSdppForwardedHeaders's doc comment for why RemoteIpAddress is wrong without it.
app.UseSdppForwardedHeaders();
app.UseSdppSecurityHeaders();

app.UseSerilogRequestLogging();
app.UseHttpMetrics(); // prometheus-net: exposes request duration/count histograms
app.UseAuthentication();
app.UseAuthorization();
app.UseSdppCsrfProtection();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var (status, title) = feature?.Error switch
    {
        DomainException domainEx => (StatusCodes.Status422UnprocessableEntity, domainEx.Message),
        FluentValidation.ValidationException validationEx =>
            (StatusCodes.Status422UnprocessableEntity, string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))),
        _ => (StatusCodes.Status500InternalServerError, "Ha ocurrido un error interno."),
    };

    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { title, traceId = context.TraceIdentifier });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapMetrics(); // /metrics for Prometheus scraping
app.MapDocumentEndpoints();
app.MapConversionEndpoints();
app.MapReportingEndpoints();

app.MapSdppHealthChecks();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Database.MigrateAsync();
}

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxProcessor<DocumentsDbContext>>(
    "documents-outbox-publisher",
    processor => processor.ProcessPendingMessagesAsync(CancellationToken.None),
    Cron.Minutely); // Hangfire's minimum built-in granularity; tighten with a custom ScheduleWakeup-style poller if sub-minute latency is required

app.Run();
