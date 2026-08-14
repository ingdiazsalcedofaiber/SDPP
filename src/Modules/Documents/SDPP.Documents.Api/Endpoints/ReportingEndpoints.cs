using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Application.UseCases.Reporting;

namespace SDPP.Documents.Api.Endpoints;

/// <summary>Real (non-mock) aggregate data for the executive/personal dashboards — see the
/// dashboard-reporting proposal. Two endpoints share the exact same query
/// (GetDocumentsOverviewQuery/IDocumentsReportingQueries): the personal one always forces
/// OwnerId to the caller's own id (never client-supplied, so a regular user cannot ask for anyone
/// else's data), the admin one always passes OwnerId=null (platform-wide) and is gated by the
/// "Administrador" policy — never both possibilities behind one client-toggleable parameter.</summary>
public static class ReportingEndpoints
{
    public static void MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents/reporting").WithTags("Reporting");

        group.MapGet("/personal", GetPersonalAsync).RequireAuthorization().WithName("GetPersonalDocumentsReport");
        group.MapGet("/admin", GetAdminAsync).RequireAuthorization("Administrador").WithName("GetAdminDocumentsReport");
    }

    private static async Task<IResult> GetPersonalAsync(
        [AsParameters] ReportingQueryParams query, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc, bucket) = ResolveRange(query);
        var result = await sender.Send(new GetDocumentsOverviewQuery(currentActor.UserId, fromUtc, toUtc, bucket), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { title = result.Error });
    }

    private static async Task<IResult> GetAdminAsync(
        [AsParameters] ReportingQueryParams query, ISender sender, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc, bucket) = ResolveRange(query);
        var result = await sender.Send(new GetDocumentsOverviewQuery(null, fromUtc, toUtc, bucket), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { title = result.Error });
    }

    public sealed class ReportingQueryParams
    {
        [FromQuery(Name = "from")] public DateTime? From { get; init; }
        [FromQuery(Name = "to")] public DateTime? To { get; init; }
        [FromQuery(Name = "bucket")] public string? Bucket { get; init; }
    }

    /// <summary>Defaults to the last 30 days, day buckets, when the caller doesn't specify —
    /// matches the "presets" the frontend's date-range filter opens with.</summary>
    private static (DateTime FromUtc, DateTime ToUtc, TimeBucket Bucket) ResolveRange(ReportingQueryParams query)
    {
        var toUtc = query.To ?? DateTime.UtcNow;
        var fromUtc = query.From ?? toUtc.AddDays(-30);
        var bucket = Enum.TryParse<TimeBucket>(query.Bucket, ignoreCase: true, out var parsed) ? parsed : TimeBucket.Day;
        return (fromUtc, toUtc, bucket);
    }
}
