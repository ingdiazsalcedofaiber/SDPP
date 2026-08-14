using Microsoft.EntityFrameworkCore;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence;

public sealed class SignerAccessChallengeRepository(SignatureDbContext dbContext) : ISignerAccessChallengeRepository
{
    public Task<SignerAccessChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.SignerAccessChallenges.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<SignerAccessChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.SignerAccessChallenges.FirstOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);

    public Task<SignerAccessChallenge?> GetByRecipientIdAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        dbContext.SignerAccessChallenges
            .Where(c => c.RecipientId == recipientId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(SignerAccessChallenge challenge) => dbContext.SignerAccessChallenges.Add(challenge);
}
