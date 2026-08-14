using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SDPP.Identity.Domain;
using SDPP.Identity.Domain.Aggregates;
using SDPP.Identity.Infrastructure.Persistence;

namespace SDPP.Identity.Infrastructure.Seeding;

/// <summary>One entry of the "Identity:SeedAllowedDomains" configuration array — bound directly
/// here rather than through IdentityPolicyOptions since only the seeder needs the per-domain
/// IsDevOnly flag, not the rest of the application layer.</summary>
public sealed record SeedAllowedDomainConfig(string Domain, bool IsDevOnly);

/// <summary>
/// Seeds the 3 system roles (exact casing every other module's RBAC already relies on — see
/// WellKnownRoles) and the configured allowed-domains list on first run. Never overwrites existing
/// rows, same "only if empty" guard every other module's seeder in this codebase already uses.
/// </summary>
public static class DefaultDataSeeder
{
    public static async Task SeedAsync(IdentityDbContext dbContext, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Roles.AnyAsync(cancellationToken))
        {
            dbContext.Roles.AddRange(
                Role.Create(WellKnownRoles.Usuario, "Acceso estándar a la plataforma.", isSystemRole: true),
                Role.Create(WellKnownRoles.Auditor, "Puede consultar la trazabilidad y auditoría.", isSystemRole: true),
                Role.Create(WellKnownRoles.Administrador, "Administra usuarios, roles y dominios permitidos.", isSystemRole: true));
        }

        if (!await dbContext.AllowedDomains.AnyAsync(cancellationToken))
        {
            var configured = configuration.GetSection("Identity:SeedAllowedDomains").Get<SeedAllowedDomainConfig[]>() ?? [];
            foreach (var entry in configured)
            {
                dbContext.AllowedDomains.Add(AllowedDomain.Create(entry.Domain, entry.IsDevOnly, notes: null));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
