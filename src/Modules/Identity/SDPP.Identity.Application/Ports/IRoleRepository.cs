using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Application.Ports;

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Role role);
}
