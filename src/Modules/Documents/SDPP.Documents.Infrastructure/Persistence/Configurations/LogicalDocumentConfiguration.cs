using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence.Configurations;

public sealed class LogicalDocumentConfiguration : IEntityTypeConfiguration<LogicalDocument>
{
    public void Configure(EntityTypeBuilder<LogicalDocument> builder)
    {
        builder.ToTable("LogicalDocuments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.OwnerId).IsRequired();
        builder.Property(d => d.CurrentVersionId).IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        // Backs DocumentsReportingQueries' "total documents" count, scoped per owner.
        builder.HasIndex(d => d.OwnerId);
    }
}
