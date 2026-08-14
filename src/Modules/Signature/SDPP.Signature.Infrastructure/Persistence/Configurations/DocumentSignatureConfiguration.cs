using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class DocumentSignatureConfiguration : IEntityTypeConfiguration<DocumentSignature>
{
    public void Configure(EntityTypeBuilder<DocumentSignature> builder)
    {
        builder.ToTable("DocumentSignatures");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.RecipientId).IsRequired();
        builder.Property(s => s.DocumentId).IsRequired();
        builder.Property(s => s.DocumentVersionId).IsRequired();
        builder.Property(s => s.DocumentHashAtSigning).HasMaxLength(64).IsRequired();
        builder.Property(s => s.CanonicalPayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.CryptographicSignatureBase64).IsRequired();
        builder.Property(s => s.PublicKeyId).IsRequired();
        builder.Property(s => s.Algorithm).HasMaxLength(20).IsRequired();
        builder.Property(s => s.TimestampUtc).IsRequired();
        builder.Property(s => s.TimestampSource).HasMaxLength(30).IsRequired();

        builder.HasIndex(s => s.RecipientId);
        builder.HasIndex(s => s.PublicKeyId);
    }
}
