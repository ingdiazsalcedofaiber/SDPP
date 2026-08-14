using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.RecipientId).IsRequired();
        builder.Property(c => c.ConsentText).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.ConsentVersion).HasMaxLength(20).IsRequired();
        builder.Property(c => c.TimestampUtc).IsRequired();
        builder.Property(c => c.IpAddress).HasMaxLength(64);
        builder.Property(c => c.UserAgent).HasMaxLength(500);
        builder.Property(c => c.AuthenticationMethod).HasMaxLength(20).IsRequired();

        builder.HasIndex(c => c.RecipientId);
    }
}
