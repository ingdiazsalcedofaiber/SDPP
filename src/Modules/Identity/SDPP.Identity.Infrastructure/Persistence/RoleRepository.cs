using Microsoft.EntityFrameworkCore;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Infrastructure.Persistence;

public sealed class RoleRepository(IdentityDbContext dbContext) : IRoleRepository
{
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Roles.ToListAsync(cancellationToken);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void Add(Role role) => dbContext.Roles.Add(role);
}
