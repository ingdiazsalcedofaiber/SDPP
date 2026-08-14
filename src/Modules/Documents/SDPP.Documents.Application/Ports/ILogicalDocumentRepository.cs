using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Application.Ports;

public interface ILogicalDocumentRepository
{
    Task<LogicalDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(LogicalDocument logicalDocument);
}
