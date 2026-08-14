namespace SDPP.BuildingBlocks.Application;

/// <summary>
/// Commits the changes made within a single use case as one transaction, including the outbox
/// messages written for any domain events raised (see SDPP.BuildingBlocks.Infrastructure.Outbox).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
