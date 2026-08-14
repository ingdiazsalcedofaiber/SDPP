using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence.Configurations;

public sealed class AllowedDomainConfiguration : IEntityTypeConfiguration<AllowedDomain>
{
    public void Configure(EntityTypeBuilder<AllowedDomain> builder)
    {
        builder.ToTable("AllowedDomains");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Domain).HasMaxLength(255).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(500);
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        builder.HasIndex(d => d.Domain).IsUnique();
    }
}
