using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SDPP.BuildingBlocks.Infrastructure.Health;

/// <summary>
/// Real dependency check (not just "the process is alive") — every module's own /health previously
/// answered { status: "healthy" } unconditionally, so a dead SQL Server never showed up until a
/// real user request failed. <c>Database.CanConnectAsync()</c> is a cheap round-trip (no query
/// against user tables), safe to run on every health probe. Deliberately reports no connection
/// string, server name, or exception detail in the response body — only a boolean-shaped status —
/// per docs/07-operations "no exponer información interna" in a health endpoint.
/// </summary>
public sealed class DbContextHealthCheck<TContext>(TContext dbContext) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
