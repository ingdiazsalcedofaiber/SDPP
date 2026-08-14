using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Documents.Application.UseCases.RequestConversion;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Api.Endpoints;

public static class ConversionEndpoints
{
    public static void MapConversionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents/{documentId:guid}/conversions")
            .RequireAuthorization()
            .WithTags("Conversions");

        group.MapPost("/", RequestAsync)
            .WithName("RequestConversion")
            .Produces<RequestConversionResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    public sealed record RequestConversionBody(OperationType OperationType, Dictionary<string, string>? OperationParameters);

    private static async Task<IResult> RequestAsync(
        [FromRoute] Guid documentId, [FromBody] RequestConversionBody body, ISender sender, CancellationToken cancellationToken)
    {
        var command = new RequestConversionCommand(documentId, body.OperationType, body.OperationParameters ?? new Dictionary<string, string>());
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/conversions/{result.Value.JobId}", result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }
}
