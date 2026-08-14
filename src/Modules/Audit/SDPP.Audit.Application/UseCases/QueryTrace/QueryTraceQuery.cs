using MediatR;
using SDPP.Audit.Application.Ports;
using SDPP.BuildingBlocks.Application;

namespace SDPP.Audit.Application.UseCases.QueryTrace;

public sealed record AuditRecordDto(
    long Id, DateTime OccurredAtUtc, string EventType, Guid ActorUserId, string ActorFullName,
    string ActorEmail, string? ActorIp, Guid? SubjectDocumentId, string RecordHash);

public sealed record QueryTraceResult(IReadOnlyList<AuditRecordDto> Items, int TotalCount, bool ChainValid);

/// <summary>Backs UC-05 (docs/04-use-cases/use-cases.md) — restricted to Auditor/Administrador
/// at the API authorization layer, never at the query layer (see docs/05-security/rbac-matrix.md).</summary>
public sealed record QueryTraceQuery(
    Guid? DocumentId, Guid? UserId, DateTime? FromUtc, DateTime? ToUtc, string? EventType, int Page, int PageSize)
    : IQuery<QueryTraceResult>;

public sealed class QueryTraceHandler(IAuditRecordRepository repository) : IRequestHandler<QueryTraceQuery, Result<QueryTraceResult>>
{
    public async Task<Result<QueryTraceResult>> Handle(QueryTraceQuery request, CancellationToken cancellationToken)
    {
        var filter = new AuditQueryFilter(
            request.DocumentId, request.UserId, request.FromUtc, request.ToUtc, request.EventType, request.Page, request.PageSize);

        var page = await repository.QueryAsync(filter, cancellationToken);

        var items = page.Items
            .Select(r => new AuditRecordDto(
                r.Id, r.OccurredAtUtc, r.EventType, r.ActorUserId, r.ActorFullName, r.ActorEmail, r.ActorIp, r.SubjectDocumentId, r.RecordHash))
            .ToList();

        return Result.Success(new QueryTraceResult(items, page.TotalCount, page.ChainValid));
    }
}
