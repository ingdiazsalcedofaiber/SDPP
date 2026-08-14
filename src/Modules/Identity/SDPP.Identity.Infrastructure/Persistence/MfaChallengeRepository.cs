using Microsoft.EntityFrameworkCore;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence;

public sealed class MfaChallengeRepository(IdentityDbContext dbContext) : IMfaChallengeRepository
{
    public Task<MfaChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.MfaChallenges.FirstOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);

    public void Add(MfaChallenge challenge) => dbContext.MfaChallenges.Add(challenge);
}
