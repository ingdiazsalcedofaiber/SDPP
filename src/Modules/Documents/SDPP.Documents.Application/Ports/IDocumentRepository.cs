using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Application.Ports;

public interface IDocumentRepository
{
    Task<DocumentInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(DocumentInstance document);
}
