using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence.Configurations;

public sealed class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("MfaChallenges");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Purpose).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();
        builder.Property(c => c.ExpiresAtUtc).IsRequired();
        builder.Property(c => c.FailedAttempts).IsRequired();

        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => new { c.UserId, c.ExpiresAtUtc });
    }
}
