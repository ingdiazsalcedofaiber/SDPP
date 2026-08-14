using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class SignatureEnvelopeConfiguration : IEntityTypeConfiguration<SignatureEnvelope>
{
    public void Configure(EntityTypeBuilder<SignatureEnvelope> builder)
    {
        builder.ToTable("SignatureEnvelopes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.OrganizationId).IsRequired();
        builder.Property(e => e.SourceDocumentId).IsRequired();
        builder.Property(e => e.SourceDocumentVersionId).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(2000);
        builder.Property(e => e.CreatedByUserId).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.SigningMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.OriginalSha256Hash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.FinalSha256Hash).HasMaxLength(64);
        builder.Property(e => e.EnvelopeHash).HasMaxLength(64);
        builder.Property(e => e.CertificateHash).HasMaxLength(64);

        builder.Navigation(e => e.Recipients).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_recipients");
        builder.HasMany(e => e.Recipients).WithOne().HasForeignKey(r => r.EnvelopeId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Fields).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_fields");
        builder.HasMany(e => e.Fields).WithOne().HasForeignKey(f => f.EnvelopeId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.DocumentSignatures).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_documentSignatures");
        builder.HasMany(e => e.DocumentSignatures).WithOne().HasForeignKey(s => s.EnvelopeId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.ConsentRecords).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_consentRecords");
        builder.HasMany(e => e.ConsentRecords).WithOne().HasForeignKey(c => c.EnvelopeId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CreatedByUserId);
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.DueDateUtc);
    }
}
