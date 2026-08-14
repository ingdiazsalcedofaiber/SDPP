using System.Security.Cryptography;
using System.Text;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

/// <summary>Shared by SubmitField/CompleteRecipientSigning/DeclineEnvelope: an internal recipient
/// (MatchedUserId set) must be signed into SDPP as that exact account — the emailed link token
/// alone was never meant to be sufficient for someone who already has real platform credentials.
/// An external recipient instead presents the bearer session token minted by VerifyOtp.</summary>
internal static class RecipientAccessAuthorization
{
    public static bool CanAct(EnvelopeRecipient recipient, SignerAccessChallenge challenge, ICurrentActor currentActor, string? sessionToken)
    {
        if (recipient.MatchedUserId is not null)
        {
            return currentActor.IsAuthenticated && currentActor.UserId == recipient.MatchedUserId;
        }

        if (string.IsNullOrEmpty(sessionToken))
        {
            return false;
        }

        var sessionTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken)));
        return challenge.ValidateSessionToken(sessionTokenHash);
    }
}
