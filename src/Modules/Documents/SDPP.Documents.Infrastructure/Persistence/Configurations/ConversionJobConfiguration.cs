using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence.Configurations;

public sealed class ConversionJobConfiguration : IEntityTypeConfiguration<ConversionJob>
{
    public void Configure(EntityTypeBuilder<ConversionJob> builder)
    {
        builder.ToTable("ConversionJobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.OperationType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(j => j.Status).HasConversion<byte>().IsRequired();
        builder.Property(j => j.EngineUsed).HasMaxLength(100);
        builder.Property(j => j.ErrorDetail).HasMaxLength(4000);
        builder.Property(j => j.CreatedAtUtc).IsRequired();

        // Backs the reporting time-series/status-breakdown queries (DocumentsReportingQueries) —
        // every one of them filters and groups by CreatedAtUtc.
        builder.HasIndex(j => j.CreatedAtUtc);
    }
}
