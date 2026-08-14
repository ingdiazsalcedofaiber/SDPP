using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Application.Ports;

public interface IDocumentVersionRepository
{
    Task<DocumentVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(DocumentVersion documentVersion);
}
