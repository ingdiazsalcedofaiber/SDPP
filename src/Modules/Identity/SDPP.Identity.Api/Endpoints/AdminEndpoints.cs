using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.UseCases.Admin.AllowedDomains;
using SDPP.Identity.Application.UseCases.Admin.ListAccessHistory;
using SDPP.Identity.Application.UseCases.Admin.ListRoles;
using SDPP.Identity.Application.UseCases.Admin.ListUsers;
using SDPP.Identity.Application.UseCases.Admin.ListUserSessions;
using SDPP.Identity.Application.UseCases.Admin.Reporting;
using SDPP.Identity.Application.UseCases.Admin.UpdateUserRoles;
using SDPP.Identity.Application.UseCases.Admin.UpdateUserStatus;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Enums;

namespace SDPP.Identity.Api.Endpoints;

/// <summary>Everything here requires the Administrador role — see the "Administrador" policy
/// registered in Program.cs (RequireRole against the exact WellKnownRoles.Administrador spelling).</summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").RequireAuthorization("Administrador").WithTags("Admin");

        group.MapGet("/users", ListUsersAsync).WithName("AdminListUsers");
        group.MapPatch("/users/{userId:guid}/roles", UpdateUserRolesAsync).WithName("AdminUpdateUserRoles");
        group.MapPatch("/users/{userId:guid}/status", UpdateUserStatusAsync).WithName("AdminUpdateUserStatus");
        group.MapGet("/users/{userId:guid}/sessions", ListUserSessionsAsync).WithName("AdminListUserSessions");

        group.MapGet("/access-history", ListAccessHistoryAsync).WithName("AdminListAccessHistory");
        group.MapGet("/roles", ListRolesAsync).WithName("AdminListRoles");
        group.MapGet("/reporting/users", GetUsersOverviewAsync).WithName("AdminGetUsersOverview");

        group.MapGet("/allowed-domains", ListAllowedDomainsAsync).WithName("AdminListAllowedDomains");
        group.MapPost("/allowed-domains", CreateAllowedDomainAsync).WithName("AdminCreateAllowedDomain");
        group.MapDelete("/allowed-domains/{id:guid}", DeleteAllowedDomainAsync).WithName("AdminDeleteAllowedDomain");
    }

    private static async Task<IResult> ListUsersAsync(
        [AsParameters] ListUsersParams query, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ListUsersQuery(query.Search, query.Domain, query.Active, query.Page ?? 1, query.PageSize ?? 25), cancellationToken);
        return Results.Ok(result.Value);
    }

    public sealed class ListUsersParams
    {
        [FromQuery(Name = "search")] public string? Search { get; init; }
        [FromQuery(Name = "domain")] public string? Domain { get; init; }
        [FromQuery(Name = "active")] public bool? Active { get; init; }
        [FromQuery(Name = "page")] public int? Page { get; init; }
        [FromQuery(Name = "pageSize")] public int? PageSize { get; init; }
    }

    public sealed record UpdateRolesBody(IReadOnlyList<Guid> RoleIds);

    private static async Task<IResult> UpdateUserRolesAsync(
        [FromRoute] Guid userId, UpdateRolesBody body, ISender sender, ICurrentActor currentActor, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateUserRolesCommand(userId, body.RoleIds, currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.Json(new { title = result.Error, errorCode = result.ErrorCode }, statusCode: StatusCodes.Status409Conflict);
    }

    public sealed record UpdateStatusBody(bool Active);

    private static async Task<IResult> UpdateUserStatusAsync(
        [FromRoute] Guid userId, UpdateStatusBody body, ISender sender, ICurrentActor currentActor, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateUserStatusCommand(userId, body.Active, currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.Json(new { title = result.Error, errorCode = result.ErrorCode }, statusCode: StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListUserSessionsAsync([FromRoute] Guid userId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListUserSessionsQuery(userId), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ListAccessHistoryAsync(
        [AsParameters] ListAccessHistoryParams query, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListAccessHistoryQuery(
            query.UserId, query.Email, query.From, query.To, query.Result, query.Page ?? 1, query.PageSize ?? 25), cancellationToken);
        return Results.Ok(result.Value);
    }

    public sealed class ListAccessHistoryParams
    {
        [FromQuery(Name = "userId")] public Guid? UserId { get; init; }
        [FromQuery(Name = "email")] public string? Email { get; init; }
        [FromQuery(Name = "from")] public DateTime? From { get; init; }
        [FromQuery(Name = "to")] public DateTime? To { get; init; }
        [FromQuery(Name = "result")] public AccessResult? Result { get; init; }
        [FromQuery(Name = "page")] public int? Page { get; init; }
        [FromQuery(Name = "pageSize")] public int? PageSize { get; init; }
    }

    private static async Task<IResult> ListRolesAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListRolesQuery(), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetUsersOverviewAsync(
        [AsParameters] ReportingQueryParams query, ISender sender, CancellationToken cancellationToken)
    {
        var (from, to, bucket) = ResolveReportingRange(query);
        var result = await sender.Send(new GetUsersOverviewQuery(from, to, bucket), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { title = result.Error });
    }

    public sealed class ReportingQueryParams
    {
        [FromQuery(Name = "from")] public DateTime? From { get; init; }
        [FromQuery(Name = "to")] public DateTime? To { get; init; }
        [FromQuery(Name = "bucket")] public string? Bucket { get; init; }
    }

    /// <summary>Defaults to the last 30 days, day buckets — mirrors Documents.Api.Endpoints.ReportingEndpoints.ResolveRange.</summary>
    private static (DateTime FromUtc, DateTime ToUtc, ReportingTimeBucket Bucket) ResolveReportingRange(ReportingQueryParams query)
    {
        var toUtc = query.To ?? DateTime.UtcNow;
        var fromUtc = query.From ?? toUtc.AddDays(-30);
        var bucket = Enum.TryParse<ReportingTimeBucket>(query.Bucket, ignoreCase: true, out var parsed) ? parsed : ReportingTimeBucket.Day;
        return (fromUtc, toUtc, bucket);
    }

    private static async Task<IResult> ListAllowedDomainsAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListAllowedDomainsQuery(), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateAllowedDomainAsync(CreateAllowedDomainCommand command, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/v1/admin/allowed-domains/{result.Value.Id}", result.Value)
            : Results.Json(new { title = result.Error, errorCode = result.ErrorCode }, statusCode: StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> DeleteAllowedDomainAsync([FromRoute] Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteAllowedDomainCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Results.NotFound(new { title = result.Error });
    }
}
