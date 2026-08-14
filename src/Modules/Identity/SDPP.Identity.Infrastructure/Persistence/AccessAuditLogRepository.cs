using Microsoft.EntityFrameworkCore;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Entities;
using SDPP.Identity.Domain.Enums;

namespace SDPP.Identity.Infrastructure.Persistence;

public sealed class AccessAuditLogRepository(IdentityDbContext dbContext) : IAccessAuditLogRepository
{
    public async Task<(IReadOnlyList<AccessAuditLogEntry> Items, int TotalCount)> SearchAsync(
        Guid? userId, string? email, DateTime? fromUtc, DateTime? toUtc, AccessResult? result,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AccessAuditLog.AsQueryable();

        if (userId is not null) query = query.Where(e => e.UserId == userId);
        if (!string.IsNullOrWhiteSpace(email)) query = query.Where(e => e.Email.Contains(email));
        if (fromUtc is not null) query = query.Where(e => e.OccurredAtUtc >= fromUtc);
        if (toUtc is not null) query = query.Where(e => e.OccurredAtUtc <= toUtc);
        if (result is not null) query = query.Where(e => e.Result == result);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(AccessAuditLogEntry entry) => dbContext.AccessAuditLog.Add(entry);
}
