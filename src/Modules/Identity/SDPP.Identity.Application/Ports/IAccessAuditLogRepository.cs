using SDPP.Identity.Domain.Entities;
using SDPP.Identity.Domain.Enums;

namespace SDPP.Identity.Application.Ports;

public interface IAccessAuditLogRepository
{
    Task<(IReadOnlyList<AccessAuditLogEntry> Items, int TotalCount)> SearchAsync(
        Guid? userId, string? email, DateTime? fromUtc, DateTime? toUtc, AccessResult? result,
        int page, int pageSize, CancellationToken cancellationToken = default);

    void Add(AccessAuditLogEntry entry);
}
