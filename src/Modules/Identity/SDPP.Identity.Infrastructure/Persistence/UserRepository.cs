using Microsoft.EntityFrameworkCore;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.Include(u => u.Roles).Include(u => u.MfaBackupCodes).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default) =>
        dbContext.Users.Include(u => u.Roles).Include(u => u.MfaBackupCodes).FirstOrDefaultAsync(u => u.GoogleId == googleId, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.Include(u => u.Roles).Include(u => u.MfaBackupCodes).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        string? search, string? domain, bool? active, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.Include(u => u.Roles).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(domain))
        {
            query = query.Where(u => u.Domain == domain);
        }
        if (active is not null)
        {
            query = query.Where(u => u.Active == active);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(User user) => dbContext.Users.Add(user);
}
