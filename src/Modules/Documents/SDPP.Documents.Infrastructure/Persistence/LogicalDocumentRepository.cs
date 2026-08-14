using Microsoft.EntityFrameworkCore;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence;

public sealed class LogicalDocumentRepository(DocumentsDbContext dbContext) : ILogicalDocumentRepository
{
    public Task<LogicalDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.LogicalDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public void Add(LogicalDocument logicalDocument) => dbContext.LogicalDocuments.Add(logicalDocument);
}
