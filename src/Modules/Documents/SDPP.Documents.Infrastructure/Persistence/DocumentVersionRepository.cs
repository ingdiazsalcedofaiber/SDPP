using Microsoft.EntityFrameworkCore;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Infrastructure.Persistence;

public sealed class DocumentVersionRepository(DocumentsDbContext dbContext) : IDocumentVersionRepository
{
    public Task<DocumentVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.DocumentVersions.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public void Add(DocumentVersion documentVersion) => dbContext.DocumentVersions.Add(documentVersion);
}
