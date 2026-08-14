using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Application.Ports;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(Session session);
}
