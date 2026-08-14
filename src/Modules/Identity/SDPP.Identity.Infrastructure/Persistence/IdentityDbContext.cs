using Microsoft.EntityFrameworkCore;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.Identity.Domain.Aggregates;
using SDPP.Identity.Domain.Entities;

namespace SDPP.Identity.Infrastructure.Persistence;

/// <summary>EF Core write-side context for the Identity bounded context, schema `identity` in its
/// own physical database (`SDPP_Identity`) — same one-database-per-module pattern already used by
/// Documents/Classification/Audit.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<AllowedDomain> AllowedDomains => Set<AllowedDomain>();
    public DbSet<AccessAuditLogEntry> AccessAuditLog => Set<AccessAuditLogEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).HasMaxLength(500).IsRequired();
            builder.Property(m => m.Content).IsRequired();
        });
    }
}
