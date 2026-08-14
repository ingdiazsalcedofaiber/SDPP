using SDPP.BuildingBlocks.Application;

namespace SDPP.Audit.Infrastructure.Persistence;

/// <summary>
/// Commits (and disposes) the transaction opened by AuditRecordRepository.GetLastRecordHashAsync
/// right after the insert — see docs/05-security/audit-and-traceability.md §2 for why the
/// locking read and the insert must be atomic.
/// </summary>
public sealed class AuditEfUnitOfWork(AuditDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await dbContext.SaveChangesAsync(cancellationToken);

        if (dbContext.PendingChainTransaction is { } transaction)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
            dbContext.PendingChainTransaction = null;
        }

        return result;
    }
}
