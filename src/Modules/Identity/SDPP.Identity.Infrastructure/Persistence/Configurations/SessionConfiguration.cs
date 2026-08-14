using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.RefreshTokenHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.IpAddress).HasMaxLength(45);
        builder.Property(s => s.UserAgent).HasMaxLength(500);
        builder.Property(s => s.OperatingSystem).HasMaxLength(100);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.ExpiresAtUtc).IsRequired();
        builder.Property(s => s.LastUsedAtUtc).IsRequired();

        builder.HasIndex(s => s.RefreshTokenHash).IsUnique();
        builder.HasIndex(s => new { s.UserId, s.ExpiresAtUtc });
    }
}
