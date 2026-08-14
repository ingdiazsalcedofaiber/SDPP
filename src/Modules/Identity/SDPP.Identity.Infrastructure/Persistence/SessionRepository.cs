using Microsoft.EntityFrameworkCore;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence;

public sealed class SessionRepository(IdentityDbContext dbContext) : ISessionRepository
{
    public Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default) =>
        dbContext.Sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash, cancellationToken);

    public async Task<IReadOnlyList<Session>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Sessions.Where(s => s.UserId == userId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Session>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await dbContext.Sessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
    }

    public void Add(Session session) => dbContext.Sessions.Add(session);
}
