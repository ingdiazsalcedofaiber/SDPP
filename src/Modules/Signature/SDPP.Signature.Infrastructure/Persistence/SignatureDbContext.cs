using Microsoft.EntityFrameworkCore;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence;

/// <summary>Write-side context for the `signature` schema.</summary>
public sealed class SignatureDbContext(DbContextOptions<SignatureDbContext> options) : DbContext(options)
{
    public DbSet<SignatureEnvelope> SignatureEnvelopes => Set<SignatureEnvelope>();
    public DbSet<EnvelopeRecipient> EnvelopeRecipients => Set<EnvelopeRecipient>();
    public DbSet<EnvelopeField> EnvelopeFields => Set<EnvelopeField>();
    public DbSet<SavedSignature> SavedSignatures => Set<SavedSignature>();
    public DbSet<SignerAccessChallenge> SignerAccessChallenges => Set<SignerAccessChallenge>();
    public DbSet<SignatureKey> SignatureKeys => Set<SignatureKey>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<InAppNotification> Notifications => Set<InAppNotification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("signature");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SignatureDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).HasMaxLength(500).IsRequired();
            builder.Property(m => m.Content).IsRequired();
        });
    }
}
