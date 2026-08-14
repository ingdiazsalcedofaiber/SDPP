using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.Notifications;

public sealed record NotificationDto(
    Guid Id, NotificationType Type, string Title, string Message, Guid? EnvelopeId, DateTime CreatedAtUtc, DateTime? ReadAtUtc);

public sealed record ListNotificationsQuery(bool UnreadOnly) : IQuery<IReadOnlyList<NotificationDto>>;

public sealed class ListNotificationsHandler(INotificationRepository repository, ICurrentActor currentActor)
    : IRequestHandler<ListNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await repository.GetByUserIdAsync(currentActor.UserId, request.UnreadOnly, cancellationToken);
        var dtos = notifications
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.EnvelopeId, n.CreatedAtUtc, n.ReadAtUtc))
            .ToList();
        return Result.Success<IReadOnlyList<NotificationDto>>(dtos);
    }
}

public sealed record MarkNotificationReadCommand(Guid NotificationId) : ICommand;

/// <summary>Ownership check here doubles as this module's only real access control for
/// notifications — a user may only ever mark their OWN notifications read.</summary>
public sealed class MarkNotificationReadHandler(
    INotificationRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor)
    : IRequestHandler<MarkNotificationReadCommand, Result>
{
    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await repository.GetByIdAsync(request.NotificationId, cancellationToken);
        if (notification is null || notification.UserId != currentActor.UserId)
        {
            return Result.Failure("La notificación no existe.", "NOTIFICATION_NOT_FOUND");
        }

        notification.MarkRead();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
