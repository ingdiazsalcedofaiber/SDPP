using Microsoft.EntityFrameworkCore;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Infrastructure.Persistence;

public sealed class NotificationRepository(SignatureDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyList<InAppNotification>> GetByUserIdAsync(Guid userId, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }
        return await query.OrderByDescending(n => n.CreatedAtUtc).Take(100).ToListAsync(cancellationToken);
    }

    public Task<InAppNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public void Add(InAppNotification notification) => dbContext.Notifications.Add(notification);
}
