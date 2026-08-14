using SDPP.BuildingBlocks.Application;

namespace SDPP.Signature.Infrastructure.Persistence;

public sealed class EfUnitOfWork(SignatureDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
