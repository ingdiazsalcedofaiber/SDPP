using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using SDPP.Audit.Domain.Aggregates;

namespace SDPP.Audit.Infrastructure.Persistence;

/// <summary>Write-side context for the `audit` schema. AuditRecords is append-only end to end —
/// see docs/03-data/er-model.md §2 and docs/05-security/audit-and-traceability.md §3: the SQL
/// login this context connects with is granted only INSERT/SELECT on the table, with UPDATE and
/// DELETE explicitly denied (configured in the migration, not here — a compromised application
/// process cannot self-grant its way around a DB-level DENY).</summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    /// <summary>Holds the transaction opened by <see cref="Persistence.AuditRecordRepository.GetLastRecordHashAsync"/>
    /// so the locking read and the subsequent insert commit atomically — see the hash-chain
    /// concurrency note in docs/05-security/audit-and-traceability.md §2.</summary>
    internal IDbContextTransaction? PendingChainTransaction { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.Entity<AuditRecord>(ConfigureAuditRecord);
    }

    private static void ConfigureAuditRecord(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd(); // IDENTITY — guarantees insertion order for the chain

        // SQL Server's datetime2 carries no timezone/Kind information, so EF Core reads every
        // OccurredAtUtc back as DateTimeKind.Unspecified — DateTime.ToString("O") then omits the
        // trailing "Z" that was present when AuditRecord.ComputeHash first ran at insert time
        // (DateTime.UtcNow, Kind=Utc). Without forcing Kind back to Utc here, every record's own
        // hash silently fails to re-verify after a round trip through the database, which is
        // exactly what surfaced as chainValid:false on /api/v1/audit/records — not a real broken
        // chain, just a Kind mismatch in the hash input string.
        builder.Property(r => r.OccurredAtUtc)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.Property(r => r.EventType).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ActorFullName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ActorEmail).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ActorDomain).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ActorIp).HasMaxLength(45);
        builder.Property(r => r.ActorMac).HasMaxLength(17);
        builder.Property(r => r.ActorHostname).HasMaxLength(200);
        builder.Property(r => r.ActorOs).HasMaxLength(200);
        builder.Property(r => r.ActorUserAgent).HasMaxLength(500);
        builder.Property(r => r.PayloadJson).IsRequired();
        builder.Property(r => r.PreviousRecordHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.RecordHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(r => new { r.SubjectDocumentId, r.OccurredAtUtc });
        builder.HasIndex(r => new { r.ActorUserId, r.OccurredAtUtc });
    }
}
