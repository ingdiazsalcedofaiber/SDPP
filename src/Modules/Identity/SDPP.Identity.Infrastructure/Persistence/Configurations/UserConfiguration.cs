using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.GoogleId).HasMaxLength(255).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.PhotoUrl).HasMaxLength(500);
        builder.Property(u => u.Domain).HasMaxLength(255).IsRequired();
        builder.Property(u => u.Department).HasMaxLength(200);
        builder.Property(u => u.CreatedAtUtc).IsRequired();
        builder.Property(u => u.UpdatedAtUtc).IsRequired();
        builder.Property(u => u.MfaEnabled).IsRequired();
        builder.Property(u => u.MfaSecretEncrypted);

        builder.HasIndex(u => u.GoogleId).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();

        // Backs UsersReportingQueries' new-registrations time series, which groups by CreatedAtUtc.
        builder.HasIndex(u => u.CreatedAtUtc);

        builder.Navigation(u => u.Roles)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_roles");

        builder.HasMany(u => u.Roles)
            .WithOne()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.MfaBackupCodes)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_mfaBackupCodes");

        builder.HasMany(u => u.MfaBackupCodes)
            .WithOne()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MfaBackupCodeConfiguration : IEntityTypeConfiguration<Domain.Entities.MfaBackupCode>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.MfaBackupCode> builder)
    {
        builder.ToTable("MfaBackupCodes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        builder.HasIndex(c => new { c.UserId, c.CodeHash });
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<Domain.Entities.UserRole>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).ValueGeneratedNever();
        builder.Property(ur => ur.AssignedAtUtc).IsRequired();

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
    }
}
