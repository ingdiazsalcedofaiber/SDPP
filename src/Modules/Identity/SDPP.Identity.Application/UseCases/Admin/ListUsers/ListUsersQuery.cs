using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.Admin.ListUsers;

public sealed record UserSummaryDto(
    Guid Id, string FullName, string Email, string? PhotoUrl, string Domain, bool Active,
    DateTime CreatedAtUtc, DateTime? LastLoginAtUtc, IReadOnlyList<string> Roles);

public sealed record ListUsersResult(IReadOnlyList<UserSummaryDto> Items, int TotalCount, int Page, int PageSize);

public sealed record ListUsersQuery(string? Search, string? Domain, bool? Active, int Page = 1, int PageSize = 25)
    : IQuery<ListUsersResult>;

public sealed class ListUsersHandler(IUserRepository userRepository, IRoleRepository roleRepository)
    : IRequestHandler<ListUsersQuery, Result<ListUsersResult>>
{
    public async Task<Result<ListUsersResult>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await userRepository.SearchAsync(
            request.Search, request.Domain, request.Active, request.Page, request.PageSize, cancellationToken);
        var allRoles = await roleRepository.GetAllAsync(cancellationToken);

        var dtos = items.Select(u => new UserSummaryDto(
            u.Id, u.FullName, u.Email, u.PhotoUrl, u.Domain, u.Active, u.CreatedAtUtc, u.LastLoginAtUtc,
            u.Roles.Select(ur => allRoles.FirstOrDefault(r => r.Id == ur.RoleId)?.Name ?? "?").ToList())).ToList();

        return Result.Success(new ListUsersResult(dtos, totalCount, request.Page, request.PageSize));
    }
}
