using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.LogicalDocumentId).IsRequired();
        builder.Property(v => v.VersionNumber).IsRequired();
        builder.Property(v => v.ChangeTypeFromPrevious).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(v => v.PreviousVersionId);
        builder.Property(v => v.CreatedByUserId).IsRequired();
        builder.Property(v => v.CreatedAtUtc).IsRequired();

        builder.HasIndex(v => v.LogicalDocumentId);
        builder.HasIndex(v => new { v.LogicalDocumentId, v.VersionNumber }).IsUnique();
    }
}
