using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Application.Ports;

public interface IMfaChallengeRepository
{
    Task<MfaChallenge?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    void Add(MfaChallenge challenge);
}
