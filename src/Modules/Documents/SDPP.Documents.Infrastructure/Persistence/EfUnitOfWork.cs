using SDPP.BuildingBlocks.Application;

namespace SDPP.Documents.Infrastructure.Persistence;

public sealed class EfUnitOfWork(DocumentsDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
