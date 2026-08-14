using MediatR;
using SDPP.Signature.Application.UseCases.Dashboard;

namespace SDPP.Signature.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/signature/dashboard/summary", GetSummaryAsync)
            .RequireAuthorization()
            .WithName("GetSignatureDashboardSummary")
            .WithTags("Dashboard")
            .Produces<EnvelopeDashboardSummary>();
    }

    private static async Task<IResult> GetSummaryAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDashboardSummaryQuery(), cancellationToken);
        return Results.Ok(result.Value);
    }
}
