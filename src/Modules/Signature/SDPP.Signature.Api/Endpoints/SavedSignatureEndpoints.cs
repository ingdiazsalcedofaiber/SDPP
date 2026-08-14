using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Signature.Application.UseCases.SavedSignatures;

namespace SDPP.Signature.Api.Endpoints;

public static class SavedSignatureEndpoints
{
    public static void MapSavedSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/signature/saved-signatures").RequireAuthorization().WithTags("SavedSignatures");

        group.MapGet("/", ListAsync).WithName("ListSavedSignatures").Produces<IReadOnlyList<SavedSignatureDto>>();
        group.MapPost("/", AddAsync).WithName("AddSavedSignature").Produces<AddSavedSignatureResult>(StatusCodes.Status201Created);
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteSavedSignature");
        group.MapGet("/{id:guid}/image", GetImageAsync).WithName("GetSavedSignatureImage");
    }

    private static async Task<IResult> GetImageAsync([FromRoute] Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSavedSignatureImageQuery(id), cancellationToken);
        return result.IsSuccess
            ? Results.File(result.Value.ImageBytes, "image/png")
            : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private sealed record AddSavedSignatureBody(string ImageBase64, double AspectRatio, string Label);

    private static async Task<IResult> ListAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListSavedSignaturesQuery(), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> AddAsync([FromBody] AddSavedSignatureBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddSavedSignatureCommand(Convert.FromBase64String(body.ImageBase64), body.AspectRatio, body.Label), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/v1/signature/saved-signatures/{result.Value.Id}", result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> DeleteAsync([FromRoute] Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSavedSignatureCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }
}
