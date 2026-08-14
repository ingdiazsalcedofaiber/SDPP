using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentInstanceConfiguration : IEntityTypeConfiguration<DocumentInstance>
{
    public void Configure(EntityTypeBuilder<DocumentInstance> builder)
    {
        builder.ToTable("DocumentInstances");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.OwnerId).IsRequired();
        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(d => d.SizeBytes).IsRequired();
        builder.Property(d => d.Status).HasConversion<byte>().IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.CreatedBy).IsRequired();
        builder.Property(d => d.DocumentVersionId).IsRequired();
        builder.Property(d => d.ConvertedFromInstanceId);

        builder.OwnsOne(d => d.StorageLocation, sp =>
        {
            sp.Property(p => p.Bucket).HasColumnName("StorageBucket").HasMaxLength(100).IsRequired();
            sp.Property(p => p.ObjectKey).HasColumnName("StorageObjectKey").HasMaxLength(1000).IsRequired();
        });

        builder.Navigation(d => d.Jobs)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_jobs");

        builder.HasMany(d => d.Jobs)
            .WithOne()
            .HasForeignKey(j => j.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.DocumentVersionId);
        builder.HasIndex(d => d.ConvertedFromInstanceId);

        // Backs DocumentsReportingQueries — content-type breakdown and storage totals both filter
        // by OwnerId, several also range-filter by CreatedAtUtc.
        builder.HasIndex(d => new { d.OwnerId, d.CreatedAtUtc });

        // byte[] is the only CLR type SQL Server's provider maps to the native ROWVERSION type
        // with real auto-generation on every write; a uint/long here compiles but leaves SQL
        // Server with no way to populate the column, so every insert fails NOT NULL.
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
