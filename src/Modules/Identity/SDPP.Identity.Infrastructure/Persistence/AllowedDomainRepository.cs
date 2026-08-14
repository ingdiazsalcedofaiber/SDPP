using Microsoft.EntityFrameworkCore;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence;

public sealed class AllowedDomainRepository(IdentityDbContext dbContext) : IAllowedDomainRepository
{
    public async Task<IReadOnlyList<AllowedDomain>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.AllowedDomains.ToListAsync(cancellationToken);

    public Task<AllowedDomain?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AllowedDomains.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<AllowedDomain?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default) =>
        dbContext.AllowedDomains.FirstOrDefaultAsync(d => d.Domain == domain.ToLower(), cancellationToken);

    public async Task<bool> IsAllowedAsync(string domain, bool includeDevOnly, CancellationToken cancellationToken = default)
    {
        var normalized = domain.ToLowerInvariant();
        return await dbContext.AllowedDomains.AnyAsync(
            d => d.Domain == normalized && d.IsActive && (includeDevOnly || !d.IsDevOnly), cancellationToken);
    }

    public void Add(AllowedDomain allowedDomain) => dbContext.AllowedDomains.Add(allowedDomain);

    public void Remove(AllowedDomain allowedDomain) => dbContext.AllowedDomains.Remove(allowedDomain);
}
