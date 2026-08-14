using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Application.Services;

namespace SDPP.Identity.Application.UseCases.GetCurrentUser;

/// <summary>Backs GET /api/v1/auth/me — the JWT's own claims are enough for authorization
/// (HttpCurrentActor), but the frontend also needs the photo URL, which was never worth putting
/// in every access token's claim set, so this re-reads the full profile once per page load.</summary>
public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<UserDto>;

public sealed class GetCurrentUserHandler(IUserRepository userRepository, IRoleRepository roleRepository)
    : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserDto>("El usuario no existe.", "USER_NOT_FOUND");
        }

        var allRoles = await roleRepository.GetAllAsync(cancellationToken);
        var roleNames = user.Roles.Select(ur => allRoles.First(r => r.Id == ur.RoleId).Name).ToList();

        return Result.Success(new UserDto(user.Id, user.FullName, user.Email, user.PhotoUrl, user.Domain, roleNames));
    }
}
