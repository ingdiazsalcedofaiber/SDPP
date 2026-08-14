using Microsoft.EntityFrameworkCore;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.Classification.Domain.Aggregates;

namespace SDPP.Classification.Infrastructure.Persistence;

/// <summary>Write-side context for the `classification` schema (docs/03-data/er-model.md).</summary>
public sealed class ClassificationDbContext(DbContextOptions<ClassificationDbContext> options) : DbContext(options)
{
    public DbSet<InspectionResult> InspectionResults => Set<InspectionResult>();
    public DbSet<ClassificationPolicy> ClassificationPolicies => Set<ClassificationPolicy>();
    public DbSet<DlpRule> DlpRules => Set<DlpRule>();
    public DbSet<DocumentIntegrityRecord> DocumentIntegrityRecords => Set<DocumentIntegrityRecord>();
    public DbSet<DocumentVersionFingerprint> DocumentVersionFingerprints => Set<DocumentVersionFingerprint>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("classification");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClassificationDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).HasMaxLength(500).IsRequired();
            builder.Property(m => m.Content).IsRequired();
        });
    }
}
