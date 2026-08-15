using Microsoft.EntityFrameworkCore;
using Prometheus;
using SDPP.Audit.Api.Endpoints;
using SDPP.Audit.Application;
using SDPP.Audit.Infrastructure;
using SDPP.Audit.Infrastructure.Persistence;
using SDPP.BuildingBlocks.Infrastructure.Health;
using SDPP.BuildingBlocks.Infrastructure.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "SDPP.Audit.Api"));

// See SDPP.Documents.Api/Program.cs — same shared cookie-JWT auth every service uses now.
builder.Services.AddSdppCookieJwtBearer(builder.Configuration);
builder.Services.AddSdppTokenRevocation(builder.Configuration);
builder.Services.AddSdppCurrentActor();

// Only Auditor/Administrador may query the trail — see docs/05-security/rbac-matrix.md.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AuditorOrAdmin", policy => policy.RequireRole("Auditor", "Administrador"));

builder.Services.AddAuditApplication();
builder.Services.AddAuditInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddSdppDatabaseHealthCheck<AuditDbContext>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    // Forces every DateTime/DateTime? in every JSON response to serialize with a "Z" (UTC) suffix —
    // see UtcDateTimeJsonConverter's doc comment for the exact bug this fixes.
    options.SerializerOptions.Converters.Add(new SDPP.BuildingBlocks.Infrastructure.Serialization.UtcDateTimeJsonConverter());
    options.SerializerOptions.Converters.Add(new SDPP.BuildingBlocks.Infrastructure.Serialization.UtcNullableDateTimeJsonConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapMetrics();
app.MapAuditEndpoints();
app.MapSdppHealthChecks();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
}

app.Run();
