using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.Admin.ListUserSessions;

public sealed record SessionDto(
    Guid Id, string? IpAddress, string? UserAgent, string? OperatingSystem,
    DateTime CreatedAtUtc, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc, DateTime LastUsedAtUtc, bool IsActive);

public sealed record ListUserSessionsQuery(Guid UserId) : IQuery<IReadOnlyList<SessionDto>>;

public sealed class ListUserSessionsHandler(ISessionRepository sessionRepository)
    : IRequestHandler<ListUserSessionsQuery, Result<IReadOnlyList<SessionDto>>>
{
    public async Task<Result<IReadOnlyList<SessionDto>>> Handle(ListUserSessionsQuery request, CancellationToken cancellationToken)
    {
        var sessions = await sessionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        IReadOnlyList<SessionDto> dtos = sessions
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new SessionDto(
                s.Id, s.IpAddress, s.UserAgent, s.OperatingSystem, s.CreatedAtUtc, s.ExpiresAtUtc, s.RevokedAtUtc, s.LastUsedAtUtc, s.IsActive))
            .ToList();

        return Result.Success(dtos);
    }
}
