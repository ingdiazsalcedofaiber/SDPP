using MediatR;
using SDPP.Identity.Application.UseCases.LookupUserByEmail;

namespace SDPP.Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/identity/users").RequireAuthorization().WithTags("Users");

        // Internal, service-to-service endpoint consumed by Signature.Api's SendEnvelope handler to
        // resolve whether a recipient's email belongs to an existing SDPP account. Not intended for
        // end-user use — same convention as Documents' /extracted-text and /signed-version.
        group.MapGet("/lookup", LookupByEmailAsync)
            .WithName("LookupUserByEmail")
            .Produces<UserLookupResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> LookupByEmailAsync(
        string email, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LookupUserByEmailQuery(email), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { title = result.Error });
    }
}
