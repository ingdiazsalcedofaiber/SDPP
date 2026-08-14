using Microsoft.EntityFrameworkCore;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence;

/// <summary>
/// EF Core write-side context for the Documents bounded context. Maps to the `documents` schema
/// described in docs/03-data/er-model.md. Query-side (dashboard) reads are expected to go
/// through Dapper against a read replica instead of this context (see
/// docs/01-architecture/technology-stack.md §2).
/// </summary>
public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public DbSet<DocumentInstance> Documents => Set<DocumentInstance>();
    public DbSet<LogicalDocument> LogicalDocuments => Set<LogicalDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("documents");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).HasMaxLength(500).IsRequired();
            builder.Property(m => m.Content).IsRequired();
        });
    }
}
