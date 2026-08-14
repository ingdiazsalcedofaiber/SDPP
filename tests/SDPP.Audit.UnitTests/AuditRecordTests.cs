using FluentAssertions;
using SDPP.Audit.Domain.Aggregates;
using Xunit;

namespace SDPP.Audit.UnitTests;

/// <summary>Verifies the hash-chain math from docs/05-security/audit-and-traceability.md §2.</summary>
public class AuditRecordTests
{
    private static ActorContext SampleActor() => new(
        Guid.NewGuid(), "Juan Pérez", "juan.perez@empresa.com", "EMPRESA",
        "10.0.0.5", null, "WKS-001", "Windows 11", "Mozilla/5.0");

    [Fact]
    public void First_record_chains_from_the_genesis_hash()
    {
        var record = AuditRecord.Create(
            AuditRecord.GenesisHash, "DocumentUploaded", DateTime.UtcNow, SampleActor(), Guid.NewGuid(), """{"x":1}""");

        record.PreviousRecordHash.Should().Be(AuditRecord.GenesisHash);
        record.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void Second_record_chains_from_the_first_records_hash()
    {
        var first = AuditRecord.Create(
            AuditRecord.GenesisHash, "DocumentUploaded", DateTime.UtcNow, SampleActor(), Guid.NewGuid(), """{"x":1}""");

        var second = AuditRecord.Create(
            first.RecordHash, "ConversionCompleted", DateTime.UtcNow, SampleActor(), Guid.NewGuid(), """{"x":2}""");

        second.PreviousRecordHash.Should().Be(first.RecordHash);
        second.VerifyHash().Should().BeTrue();
    }

    [Fact]
    public void Hash_is_deterministic_for_identical_inputs()
    {
        var occurredAt = DateTime.UtcNow;
        var hash1 = AuditRecord.ComputeHash(AuditRecord.GenesisHash, "DocumentUploaded", occurredAt, """{"x":1}""");
        var hash2 = AuditRecord.ComputeHash(AuditRecord.GenesisHash, "DocumentUploaded", occurredAt, """{"x":1}""");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Tampering_the_payload_breaks_hash_verification()
    {
        // Simulates what the periodic chain-validation job (docs/05-security/audit-and-traceability.md §2)
        // would catch: a record whose stored hash no longer matches its recomputed hash.
        var occurredAt = DateTime.UtcNow;
        var originalHash = AuditRecord.ComputeHash(AuditRecord.GenesisHash, "DocumentUploaded", occurredAt, """{"x":"original"}""");
        var hashWithTamperedPayload = AuditRecord.ComputeHash(AuditRecord.GenesisHash, "DocumentUploaded", occurredAt, """{"x":"tampered"}""");

        originalHash.Should().NotBe(hashWithTamperedPayload);
    }
}
