using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Identity.Domain.Entities;

namespace SDPP.Identity.Infrastructure.Persistence.Configurations;

public sealed class AccessAuditLogEntryConfiguration : IEntityTypeConfiguration<AccessAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AccessAuditLogEntry> builder)
    {
        builder.ToTable("AccessAuditLog");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.FullNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320).IsRequired();
        builder.Property(e => e.OccurredAtUtc).IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.Result).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(30).IsRequired();

        builder.HasIndex(e => e.OccurredAtUtc);
        builder.HasIndex(e => new { e.UserId, e.OccurredAtUtc });
        builder.HasIndex(e => e.Email);
    }
}
