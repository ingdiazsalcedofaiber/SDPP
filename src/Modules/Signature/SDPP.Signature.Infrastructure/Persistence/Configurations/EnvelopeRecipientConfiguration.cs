using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence.Configurations;

public sealed class EnvelopeRecipientConfiguration : IEntityTypeConfiguration<EnvelopeRecipient>
{
    public void Configure(EntityTypeBuilder<EnvelopeRecipient> builder)
    {
        builder.ToTable("EnvelopeRecipients");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Order).IsRequired();
        builder.Property(r => r.Email).HasMaxLength(320).IsRequired();
        builder.Property(r => r.FullName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.DeclineReason).HasMaxLength(1000);
        builder.Property(r => r.ViewedIpAddress).HasMaxLength(64);
        builder.Property(r => r.SignedIpAddress).HasMaxLength(64);
        builder.Property(r => r.AuthMethodUsed).HasMaxLength(20);
        builder.Property(r => r.ConsentIpAddress).HasMaxLength(64);
        builder.Property(r => r.ConsentUserAgent).HasMaxLength(500);
        builder.Property(r => r.ReminderCount).IsRequired();
        builder.Property(r => r.InPerson).IsRequired().HasDefaultValue(false);

        builder.HasIndex(r => r.MatchedUserId);
    }
}
