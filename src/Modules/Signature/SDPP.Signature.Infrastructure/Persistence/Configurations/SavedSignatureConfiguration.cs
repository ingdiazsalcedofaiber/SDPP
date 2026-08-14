using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class SavedSignatureConfiguration : IEntityTypeConfiguration<SavedSignature>
{
    public void Configure(EntityTypeBuilder<SavedSignature> builder)
    {
        builder.ToTable("SavedSignatures");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.ImageBytes).IsRequired();
        builder.Property(s => s.AspectRatio).IsRequired();
        builder.Property(s => s.Label).HasMaxLength(100).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();

        builder.HasIndex(s => s.UserId);
    }
}
