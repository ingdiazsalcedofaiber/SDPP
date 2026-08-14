using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.Admin.ListRoles;

public sealed record RoleDto(Guid Id, string Name, string? Description, bool IsSystemRole);

public sealed record ListRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

public sealed class ListRolesHandler(IRoleRepository roleRepository) : IRequestHandler<ListRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await roleRepository.GetAllAsync(cancellationToken);
        IReadOnlyList<RoleDto> dtos = roles.Select(r => new RoleDto(r.Id, r.Name, r.Description, r.IsSystemRole)).ToList();
        return Result.Success(dtos);
    }
}
