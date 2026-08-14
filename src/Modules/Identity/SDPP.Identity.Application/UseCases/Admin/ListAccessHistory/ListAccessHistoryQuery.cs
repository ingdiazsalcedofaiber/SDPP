using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain.Enums;

namespace SDPP.Identity.Application.UseCases.Admin.ListAccessHistory;

public sealed record AccessLogDto(
    long Id, Guid? UserId, string FullNameSnapshot, string Email, DateTime OccurredAtUtc,
    string? IpAddress, string? UserAgent, string Result, string Provider, int DurationMs);

public sealed record ListAccessHistoryResult(IReadOnlyList<AccessLogDto> Items, int TotalCount, int Page, int PageSize);

public sealed record ListAccessHistoryQuery(
    Guid? UserId, string? Email, DateTime? FromUtc, DateTime? ToUtc, AccessResult? Result, int Page = 1, int PageSize = 25)
    : IQuery<ListAccessHistoryResult>;

public sealed class ListAccessHistoryHandler(IAccessAuditLogRepository accessAuditLogRepository)
    : IRequestHandler<ListAccessHistoryQuery, Result<ListAccessHistoryResult>>
{
    public async Task<Result<ListAccessHistoryResult>> Handle(ListAccessHistoryQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await accessAuditLogRepository.SearchAsync(
            request.UserId, request.Email, request.FromUtc, request.ToUtc, request.Result, request.Page, request.PageSize, cancellationToken);

        var dtos = items.Select(e => new AccessLogDto(
            e.Id, e.UserId, e.FullNameSnapshot, e.Email, e.OccurredAtUtc, e.IpAddress, e.UserAgent,
            e.Result.ToString(), e.Provider, e.DurationMs)).ToList();

        return Result.Success(new ListAccessHistoryResult(dtos, totalCount, request.Page, request.PageSize));
    }
}
