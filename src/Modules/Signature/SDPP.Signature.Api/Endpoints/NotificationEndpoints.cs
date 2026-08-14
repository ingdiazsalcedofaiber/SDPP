using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Signature.Application.UseCases.Notifications;

namespace SDPP.Signature.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/signature/notifications").RequireAuthorization().WithTags("Notifications");

        group.MapGet("/", ListAsync).WithName("ListNotifications").Produces<IReadOnlyList<NotificationDto>>();
        group.MapPost("/{id:guid}/read", MarkReadAsync).WithName("MarkNotificationRead");
    }

    private static async Task<IResult> ListAsync([FromQuery] bool? unreadOnly, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListNotificationsQuery(unreadOnly ?? false), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> MarkReadAsync([FromRoute] Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkNotificationReadCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }
}
