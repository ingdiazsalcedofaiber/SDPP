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
        // RequireAuthorization with the same AllowAnonymous+InternalServiceKeyFilter pattern
        // Documents.Api uses for its own internal endpoints (see DocumentEndpoints.cs). Returns only
        // an intact/count summary, never the underlying records — the public verifier must not leak
        // actor emails/IPs/payloads.
        group.MapGet("/records/integrity", VerifyIntegrityAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyFilter>()
            .WithName("VerifyAuditIntegrity")
            .Produces<AuditIntegrityResult>();

        // Internal, service-to-service only — consumed by Signature.Api's Evidence Package export
        // (itself gated by envelope ownership, same check as the certificate download), never
        // reachable directly by a browser. Unlike /records/integrity, returns full record detail —
        // appropriate here since an Evidence Package is a private authenticated artifact, not the
        // anonymous public verifier.
        group.MapGet("/records/export", ExportRecordsAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyFilter>()
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
