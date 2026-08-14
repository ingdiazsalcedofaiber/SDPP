using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class SignerAccessChallengeConfiguration : IEntityTypeConfiguration<SignerAccessChallenge>
{
    public void Configure(EntityTypeBuilder<SignerAccessChallenge> builder)
    {
        builder.ToTable("SignerAccessChallenges");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.RecipientId).IsRequired();
        builder.Property(c => c.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.LinkExpiresAtUtc).IsRequired();
        builder.Property(c => c.OtpCodeHash).HasMaxLength(64);
        builder.Property(c => c.SessionTokenHash).HasMaxLength(64);

        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => c.RecipientId);
    }
}
