using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Security;

/// <summary>Today's only ITimestampAuthorityService — SDPP's own server clock, honestly labeled
/// SERVER_TIMESTAMP (never TRUSTED_TIMESTAMP, which would imply a certified RFC 3161 timestamping
/// authority SDPP does not have). Swappable for a real TSA-backed implementation later without any
/// caller change.</summary>
public sealed class ServerTimestampAuthorityService : ITimestampAuthorityService
{
    public SignatureTimestamp GetTimestamp() => new(DateTime.UtcNow, "SERVER_TIMESTAMP");
}
