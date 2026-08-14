using SDPP.Identity.Domain.Aggregates;

namespace SDPP.Identity.Application.Ports;

public interface IAllowedDomainRepository
{
    Task<IReadOnlyList<AllowedDomain>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AllowedDomain?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AllowedDomain?> GetByDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary><paramref name="includeDevOnly"/> must only ever be true when the caller already
    /// confirmed the API is running in Development — this repository has no notion of environments.</summary>
    Task<bool> IsAllowedAsync(string domain, bool includeDevOnly, CancellationToken cancellationToken = default);

    void Add(AllowedDomain allowedDomain);
    void Remove(AllowedDomain allowedDomain);
}
