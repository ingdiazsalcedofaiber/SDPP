using Microsoft.EntityFrameworkCore;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence;

public sealed class DocumentRepository(DocumentsDbContext dbContext) : IDocumentRepository
{
    public Task<DocumentInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Documents.Include(d => d.Jobs).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public void Add(DocumentInstance document) => dbContext.Documents.Add(document);
}
