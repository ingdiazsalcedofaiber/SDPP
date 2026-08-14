using SDPP.Documents.Domain.ValueObjects;

namespace SDPP.Documents.Application.Ports;

/// <summary>
/// Abstraction over the object storage backend (MinIO on-prem / NAS, see
/// docs/01-architecture/technology-stack.md §1). The domain and application layers never see a
/// filesystem path, only this port.
/// </summary>
public interface IBlobStorage
{
    Task SaveAsync(StoragePath path, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(StoragePath path, CancellationToken cancellationToken = default);
    Task DeleteAsync(StoragePath path, CancellationToken cancellationToken = default);
}
