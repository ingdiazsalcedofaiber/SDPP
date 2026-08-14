using SDPP.BuildingBlocks.Domain;
using SDPP.Identity.Domain.Enums;

namespace SDPP.Identity.Domain.Entities;

/// <summary>
/// One row per login/logout attempt, successful or not — the fast, locally-queryable copy the
/// admin panel reads directly. The same fact also gets published as an integration event
/// (AccessAttemptRecordedV1) into the existing hash-chained Audit module for tamper-evident
/// long-term retention; this table is not a replacement for that, just the low-latency view.
/// Deliberately not an AggregateRoot — it has no invariants to protect after creation, only ever
/// written once.
/// </summary>
public sealed class AccessAuditLogEntry : Entity<long>
{
    public Guid? UserId { get; private set; }
    public string FullNameSnapshot { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public AccessResult Result { get; private set; }
    public string Provider { get; private set; } = null!;
    public int DurationMs { get; private set; }
    public Guid? SessionId { get; private set; }

    private AccessAuditLogEntry() { } // EF Core

    public static AccessAuditLogEntry Record(
        Guid? userId, string fullNameSnapshot, string email, string? ipAddress, string? userAgent,
        AccessResult result, string provider, int durationMs, Guid? sessionId) => new()
    {
        UserId = userId,
        FullNameSnapshot = fullNameSnapshot,
        Email = email,
        OccurredAtUtc = DateTime.UtcNow,
        IpAddress = ipAddress,
        UserAgent = userAgent,
        Result = result,
        Provider = provider,
        DurationMs = durationMs,
        SessionId = sessionId,
    };
}
