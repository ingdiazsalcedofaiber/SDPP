using SDPP.BuildingBlocks.Domain;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// A recipient's explicit acceptance of the electronic-signature consent declaration — child entity
/// of SignatureEnvelope, created by SignatureEnvelope.RegisterConsent. Promotes what used to be a
/// few loose fields on EnvelopeRecipient (ConsentAcceptedAtUtc/ConsentIpAddress/ConsentUserAgent,
/// still populated for backward compatibility with existing certificate/query code) into a
/// first-class, independently identifiable evidentiary record, exactly the text and version shown
/// to the recipient at that moment — required before SignatureEnvelope.RegisterSignature allows this
/// recipient to sign, and later linked from DocumentSignature.ConsentId.
/// </summary>
public sealed class ConsentRecord : Entity<Guid>
{
    /// <summary>Verbatim text the recipient saw and accepted — never just referenced by version
    /// number, so historical consent stays legible even if the declaration wording changes later.</summary>
    public const string DeclarationText =
        "Estoy de acuerdo en utilizar medios electrónicos para firmar este documento y manifiesto mi intención de suscribirlo electrónicamente.";

    public const string CurrentVersion = "v1";

    public Guid EnvelopeId { get; private set; }
    public Guid RecipientId { get; private set; }
    public string ConsentText { get; private set; } = null!;
    public string ConsentVersion { get; private set; } = null!;
    public DateTime TimestampUtc { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string AuthenticationMethod { get; private set; } = null!;

    private ConsentRecord() { } // EF Core

    internal static ConsentRecord Create(Guid envelopeId, Guid recipientId, string? ipAddress, string? userAgent, string authenticationMethod)
    {
        return new ConsentRecord
        {
            Id = Guid.NewGuid(),
            EnvelopeId = envelopeId,
            RecipientId = recipientId,
            ConsentText = DeclarationText,
            ConsentVersion = CurrentVersion,
            TimestampUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            AuthenticationMethod = authenticationMethod,
        };
    }
}
