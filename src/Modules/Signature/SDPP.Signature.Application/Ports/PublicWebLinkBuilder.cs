namespace SDPP.Signature.Application.Ports;

/// <summary>Builds URLs into the public-facing web frontend — a config concern (the frontend's base
/// URL isn't knowable from a server-side request context, unlike the signer-access links the
/// frontend itself builds via window.location.origin), kept behind a port so Application never
/// touches IConfiguration directly.</summary>
public interface IPublicWebLinkBuilder
{
    /// <returns>The public, anonymous-access verification page URL for this envelope (see
    /// VerifyEnvelopeQuery) — printed as a QR code and plain text on the completion certificate.</returns>
    string BuildVerificationUrl(Guid envelopeId);
}
