using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using SDPP.BuildingBlocks.Domain;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.BuildingBlocks.Infrastructure.Health;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Signature.Api.Endpoints;
using SDPP.Signature.Application;
using SDPP.Signature.Infrastructure;
using SDPP.Signature.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "SDPP.Signature.Api"));

// See SDPP.Documents.Api/Program.cs — same shared cookie-JWT auth every service uses.
builder.Services.AddSdppCookieJwtBearer(builder.Configuration);
builder.Services.AddSdppTokenRevocation(builder.Configuration);
builder.Services.AddSdppCurrentActor();

builder.Services.AddSignatureApplication();
builder.Services.AddSignatureInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddSdppDatabaseHealthCheck<SignatureDbContext>();

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

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("SignatureDb"), new SqlServerStorageOptions
    {
        SchemaName = "signature_hangfire",
    }));
builder.Services.AddHangfireServer();

var app = builder.Build();

// Must run before anything that reads the client IP (logging, ICurrentActor) — see
// UseSdppForwardedHeaders's doc comment for why RemoteIpAddress is wrong without it.
app.UseSdppForwardedHeaders();
app.UseSdppSecurityHeaders();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();
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

app.MapMetrics();
app.MapEnvelopeEndpoints();
app.MapSignerAccessEndpoints();
app.MapSavedSignatureEndpoints();
app.MapVerificationEndpoints();
app.MapNotificationEndpoints();
app.MapDashboardEndpoints();
app.MapSdppHealthChecks();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SignatureDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxProcessor<SignatureDbContext>>(
    "signature-outbox-publisher",
    processor => processor.ProcessPendingMessagesAsync(CancellationToken.None),
    Cron.Minutely);

// Makes SignatureEnvelope.Expire()/GetPastDueAsync/SignatureEnvelopeExpiredV1 actually run, plus the
// recipient-reminder cadence — see EnvelopeLifecycleJob's doc comment.
app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<SDPP.Signature.Application.UseCases.EnvelopeLifecycle.EnvelopeLifecycleJob>(
    "signature-envelope-lifecycle",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly);

app.Run();
