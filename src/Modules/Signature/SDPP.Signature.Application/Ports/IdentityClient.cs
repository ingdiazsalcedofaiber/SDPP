namespace SDPP.Signature.Application.Ports;

public sealed record IdentityUserLookup(Guid UserId, string FullName);

/// <summary>
/// Outbound HTTP port to Identity.Api — used only by SendEnvelope to resolve whether a recipient's
/// email belongs to an existing SDPP account before deciding between the internal-session flow and
/// the external magic-link+OTP flow. Mirrors IDocumentsClient's HTTP-client-port shape.
/// </summary>
public interface IIdentityClient
{
    Task<IdentityUserLookup?> LookupByEmailAsync(string email, CancellationToken cancellationToken = default);
}
