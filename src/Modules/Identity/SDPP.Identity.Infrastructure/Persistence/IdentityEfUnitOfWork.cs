using SDPP.BuildingBlocks.Application;

namespace SDPP.Identity.Infrastructure.Persistence;

public sealed class IdentityEfUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
