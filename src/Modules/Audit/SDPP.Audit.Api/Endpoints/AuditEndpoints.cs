using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Audit.Application.UseCases.ExportRecords;
using SDPP.Audit.Application.UseCases.QueryTrace;
using SDPP.Audit.Application.UseCases.VerifyIntegrity;
using SDPP.BuildingBlocks.Infrastructure.Security;

namespace SDPP.Audit.Api.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit")
            .RequireAuthorization("AuditorOrAdmin") // see docs/05-security/rbac-matrix.md — audit.query
            .WithTags("Audit");

        group.MapGet("/records", QueryAsync)
            .WithName("QueryAuditTrace")
            .Produces<QueryTraceResult>();

        // Internal, service-to-service only (never reachable by a browser session, even an
        // Auditor's) — consumed by Signature.Api's public envelope verifier. Overrides the group's
        // RequireAuthorization with InternalServiceKeyOnlyFilter (NOT InternalServiceKeyFilter —
        // that one also accepts a bare authenticated session, which would have let ANY logged-in
        // user, not just Auditor/Administrador, bypass the group's AuditorOrAdmin policy via
        // .AllowAnonymous() and reach this directly; see InternalServiceKeyOnlyFilter's doc
        // comment). Returns only an intact/count summary, never the underlying records — the public
        // verifier must not leak actor emails/IPs/payloads.
        group.MapGet("/records/integrity", VerifyIntegrityAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyOnlyFilter>()
            .WithName("VerifyAuditIntegrity")
            .Produces<AuditIntegrityResult>();

        // Internal, service-to-service only — consumed by Signature.Api's Evidence Package export
        // (itself gated by envelope ownership, same check as the certificate download), never
        // reachable directly by a browser. InternalServiceKeyOnlyFilter for the same reason as
        // /records/integrity above — this returns full record detail (actor emails/IPs/payloads),
        // so a bare-session bypass here was the more serious half of that same bug.
        group.MapGet("/records/export", ExportRecordsAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyOnlyFilter>()
            .WithName("ExportAuditRecords")
            .Produces<IReadOnlyList<AuditRecordExportDto>>();
    }

    private static async Task<IResult> VerifyIntegrityAsync(
        [FromQuery(Name = "subjectId")] Guid[] subjectIds, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new VerifyIntegrityQuery(subjectIds), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> ExportRecordsAsync(
        [FromQuery(Name = "subjectId")] Guid[] subjectIds, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRecordsBySubjectsQuery(subjectIds), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> QueryAsync(
        [AsParameters] QueryParams query, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new QueryTraceQuery(
            query.DocumentId, query.UserId, query.From, query.To, query.EventType,
            query.Page ?? 1, query.PageSize ?? 25), cancellationToken);

        return Results.Ok(result.Value);
    }

    public sealed class QueryParams
    {
        [FromQuery(Name = "documentId")] public Guid? DocumentId { get; init; }
        [FromQuery(Name = "userId")] public Guid? UserId { get; init; }
        [FromQuery(Name = "from")] public DateTime? From { get; init; }
        [FromQuery(Name = "to")] public DateTime? To { get; init; }
        [FromQuery(Name = "eventType")] public string? EventType { get; init; }
        [FromQuery(Name = "page")] public int? Page { get; init; }
        [FromQuery(Name = "pageSize")] public int? PageSize { get; init; }
    }
}
