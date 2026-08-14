using System.Security.Cryptography;
using System.Text;
using SDPP.BuildingBlocks.Domain;

namespace SDPP.Audit.Domain.Aggregates;

public sealed record ActorContext(
    Guid UserId, string FullName, string Email, string Domain,
    string? IpAddress, string? MacAddress, string? Hostname, string? OperatingSystem, string? UserAgent);

/// <summary>
/// Append-only audit event, chained by hash (docs/05-security/audit-and-traceability.md §2).
/// There is intentionally no method here that mutates a persisted record — the type only ever
/// supports Create. Immutability at the database level (DENY UPDATE/DELETE, INSTEAD OF triggers)
/// is configured in SDPP.Audit.Infrastructure and is what makes this guarantee real, not just a
/// convention in the object model (see docs/05-security/audit-and-traceability.md §3).
/// </summary>
public sealed class AuditRecord : Entity<long>
{
    public Guid RecordGuid { get; private init; }
    public DateTime OccurredAtUtc { get; private init; }
    public string EventType { get; private init; } = null!;

    public Guid ActorUserId { get; private init; }
    public string ActorFullName { get; private init; } = null!;
    public string ActorEmail { get; private init; } = null!;
    public string ActorDomain { get; private init; } = null!;
    public string? ActorIp { get; private init; }
    public string? ActorMac { get; private init; }
    public string? ActorHostname { get; private init; }
    public string? ActorOs { get; private init; }
    public string? ActorUserAgent { get; private init; }

    public Guid? SubjectDocumentId { get; private init; }
    public string PayloadJson { get; private init; } = null!;

    public string PreviousRecordHash { get; private init; } = null!;
    public string RecordHash { get; private init; } = null!;

    private AuditRecord() { } // EF Core

    public static AuditRecord Create(
        string previousRecordHash, string eventType, DateTime occurredAtUtc,
        ActorContext actor, Guid? subjectDocumentId, string payloadJson)
    {
        var recordHash = ComputeHash(previousRecordHash, eventType, occurredAtUtc, payloadJson);

        return new AuditRecord
        {
            RecordGuid = Guid.NewGuid(),
            OccurredAtUtc = occurredAtUtc,
            EventType = eventType,
            ActorUserId = actor.UserId,
            ActorFullName = actor.FullName,
            ActorEmail = actor.Email,
            ActorDomain = actor.Domain,
            ActorIp = actor.IpAddress,
            ActorMac = actor.MacAddress,
            ActorHostname = actor.Hostname,
            ActorOs = actor.OperatingSystem,
            ActorUserAgent = actor.UserAgent,
            SubjectDocumentId = subjectDocumentId,
            PayloadJson = payloadJson,
            PreviousRecordHash = previousRecordHash,
            RecordHash = recordHash,
        };
    }

    /// <summary>
    /// RecordHash[n] = SHA256(RecordHash[n-1] || EventType[n] || OccurredAtUtc[n] || PayloadJson[n]),
    /// exactly as specified in docs/05-security/audit-and-traceability.md §2. Exposed as a public
    /// static method so a verification job (or an external auditor with just the exported
    /// evidence) can recompute and compare it independently of this class' persistence.
    /// </summary>
    public static string ComputeHash(string previousRecordHash, string eventType, DateTime occurredAtUtc, string payloadJson)
    {
        var input = string.Concat(previousRecordHash, eventType, occurredAtUtc.ToString("O"), payloadJson);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>64 hex zeros — the seed value for RecordHash[-1], fixed at first deployment (see
    /// docs/05-security/audit-and-traceability.md §2).</summary>
    public static readonly string GenesisHash = new('0', 64);

    public bool VerifyHash() =>
        RecordHash == ComputeHash(PreviousRecordHash, EventType, OccurredAtUtc, PayloadJson);
}
