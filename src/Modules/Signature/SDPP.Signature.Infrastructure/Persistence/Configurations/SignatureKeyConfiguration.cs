using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class SignatureKeyConfiguration : IEntityTypeConfiguration<SignatureKey>
{
    public void Configure(EntityTypeBuilder<SignatureKey> builder)
    {
        builder.ToTable("SignatureKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();

        builder.Property(k => k.Algorithm).HasMaxLength(20).IsRequired();
        builder.Property(k => k.PublicKeyBase64).IsRequired();
        builder.Property(k => k.EncryptedPrivateKey).IsRequired();
        builder.Property(k => k.CreatedAtUtc).IsRequired();
        builder.Property(k => k.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(k => k.Status);
    }
}
