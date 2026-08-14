using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Application.Ports;

public interface ISignatureEnvelopeRepository
{
    Task<SignatureEnvelope?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Resolves the owning envelope directly from one of its recipients' ids — the public
    /// signer-access flow only ever has a RecipientId (from SignerAccessChallenge), never the
    /// envelope id itself.</summary>
    Task<SignatureEnvelope?> GetByRecipientIdAsync(Guid recipientId, CancellationToken cancellationToken = default);

    /// <summary>scope: "sent" (CreatedByUserId == userId), "pending" (a recipient with
    /// MatchedUserId == userId whose turn it currently is), "all" (either of the above). Also
    /// filters by organizationId — multitenant isolation, backend-enforced (see
    /// IOrganizationContextProvider).</summary>
    Task<IReadOnlyList<SignatureEnvelope>> SearchAsync(Guid userId, string scope, Guid organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SignatureEnvelope>> GetPastDueAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>Every envelope still in a non-terminal state — backs the (Fase I) reminder pass,
    /// which needs to inspect each envelope's own recipients for reminder eligibility (see
    /// SignatureEnvelope.GetRecipientsDueForReminder).</summary>
    Task<IReadOnlyList<SignatureEnvelope>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Every envelope where the user is creator OR any recipient, REGARDLESS of status —
    /// backs the dashboard (see Fase J), which needs terminal-state envelopes too (Completed, etc.)
    /// unlike SearchAsync's "all" scope, which deliberately excludes terminal envelopes for a
    /// non-creator recipient (that scope's job is "what needs my attention", not "everything I've
    /// ever touched").</summary>
    Task<IReadOnlyList<SignatureEnvelope>> GetInvolvingUserAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

    void Add(SignatureEnvelope envelope);
}

public interface ISavedSignatureRepository
{
    Task<SavedSignature?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedSignature>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(SavedSignature signature);
    void Remove(SavedSignature signature);
}

public interface ISignerAccessChallengeRepository
{
    Task<SignerAccessChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SignerAccessChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<SignerAccessChallenge?> GetByRecipientIdAsync(Guid recipientId, CancellationToken cancellationToken = default);
    void Add(SignerAccessChallenge challenge);
}

/// <summary>Backs IKeyManagementService's DatabaseKeyManagementService implementation — see
/// SignatureKey's doc comment for why this is its own aggregate, independent of SignatureEnvelope.</summary>
public interface ISignatureKeyRepository
{
    Task<SignatureKey?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<SignatureKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(SignatureKey key);
}

public interface INotificationRepository
{
    Task<IReadOnlyList<InAppNotification>> GetByUserIdAsync(Guid userId, bool unreadOnly, CancellationToken cancellationToken = default);
    Task<InAppNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(InAppNotification notification);
}
