using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class EnvelopeFieldConfiguration : IEntityTypeConfiguration<EnvelopeField>
{
    public void Configure(EntityTypeBuilder<EnvelopeField> builder)
    {
        builder.ToTable("EnvelopeFields");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.RecipientId).IsRequired();
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.PageNumber).IsRequired();
        builder.Property(f => f.PositionX).IsRequired();
        builder.Property(f => f.PositionY).IsRequired();
        builder.Property(f => f.Width).IsRequired();
        builder.Property(f => f.Height).IsRequired();
        builder.Property(f => f.Required).IsRequired();
        builder.Property(f => f.Value).HasMaxLength(2000);
        builder.Property(f => f.SignatureMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.SignatureHash).HasMaxLength(64);

        builder.HasIndex(f => f.RecipientId);
    }
}
