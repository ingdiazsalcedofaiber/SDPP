using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace SDPP.BuildingBlocks.Infrastructure.Health;

public static class HealthCheckExtensions
{
    /// <summary>Registers a real SQL Server connectivity probe under the "database" health check
    /// name — see DbContextHealthCheck's doc comment for why this replaces the old
    /// always-"healthy" /health.</summary>
    public static IHealthChecksBuilder AddSdppDatabaseHealthCheck<TContext>(this IHealthChecksBuilder builder)
        where TContext : DbContext =>
        builder.AddCheck<DbContextHealthCheck<TContext>>("database");

    /// <summary>Same response shape the old static /health always returned ({"status": "healthy"}),
    /// now actually reflecting the registered checks — plus a per-check breakdown, never a
    /// connection string, server name, or exception message (see DbContextHealthCheck).</summary>
    public static IEndpointConventionBuilder MapSdppHealthChecks(this WebApplication app) =>
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (httpContext, report) =>
            {
                httpContext.Response.ContentType = "application/json";
                var payload = new
                {
                    status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy",
                    checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString().ToLowerInvariant()),
                };
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload));
            },
        }).AllowAnonymous();
}
