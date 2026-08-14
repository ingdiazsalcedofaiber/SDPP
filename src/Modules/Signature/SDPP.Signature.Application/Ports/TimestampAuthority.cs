namespace SDPP.Signature.Application.Ports;

public sealed record SignatureTimestamp(DateTime TimestampUtc, string Source);

/// <summary>
/// Where a DocumentSignature's timestamp comes from — deliberately abstracted so a real RFC 3161
/// timestamping authority (TRUSTED_TIMESTAMP) can be plugged in later without touching any caller.
/// Today's only implementation (ServerTimestampAuthorityService) returns SERVER_TIMESTAMP: SDPP's
/// own clock, honestly labeled as such — never printed or claimed as a certified/trusted timestamp
/// anywhere in the certificate or evidence package (see DocumentSignature.TimestampSource).
/// </summary>
public interface ITimestampAuthorityService
{
    SignatureTimestamp GetTimestamp();
}
