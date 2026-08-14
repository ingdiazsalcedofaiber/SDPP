using SDPP.BuildingBlocks.Application;

namespace SDPP.Classification.Infrastructure.Persistence;

public sealed class EfUnitOfWork(ClassificationDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
