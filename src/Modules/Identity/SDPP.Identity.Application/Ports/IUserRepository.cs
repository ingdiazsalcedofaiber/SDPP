using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Application.Ports;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        string? search, string? domain, bool? active, int page, int pageSize, CancellationToken cancellationToken = default);

    void Add(User user);
}
