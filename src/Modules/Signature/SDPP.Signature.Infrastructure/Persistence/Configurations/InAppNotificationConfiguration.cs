using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class InAppNotificationConfiguration : IEntityTypeConfiguration<InAppNotification>
{
    public void Configure(EntityTypeBuilder<InAppNotification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.CreatedAtUtc).IsRequired();

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => n.ReadAtUtc);
    }
}
