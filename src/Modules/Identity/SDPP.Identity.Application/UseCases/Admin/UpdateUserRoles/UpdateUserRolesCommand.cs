using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Identity;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Domain;

namespace SDPP.Identity.Application.UseCases.Admin.UpdateUserRoles;

public sealed record UpdateUserRolesCommand(Guid UserId, IReadOnlyList<Guid> RoleIds, Guid ChangedByUserId) : ICommand;

/// <summary>Blocks an administrator from removing their own Administrador role — the aggregate
/// itself has no notion of role names, so this guard lives here, where both the acting user and
/// the resolved role names are available.</summary>
public sealed class UpdateUserRolesHandler(
    IUserRepository userRepository, IRoleRepository roleRepository, IUnitOfWork unitOfWork,
    IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<UpdateUserRolesCommand, Result>
{
    public async Task<Result> Handle(UpdateUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("El usuario no existe.", "USER_NOT_FOUND");
        }

        var allRoles = await roleRepository.GetAllAsync(cancellationToken);
        var newRoleNames = allRoles.Where(r => request.RoleIds.Contains(r.Id)).Select(r => r.Name).ToList();

        if (request.UserId == request.ChangedByUserId && !newRoleNames.Contains(WellKnownRoles.Administrador))
        {
            return Result.Failure(
                "No puedes quitarte a ti mismo el rol Administrador — pide a otro administrador que lo haga.", "SELF_DEMOTION_BLOCKED");
        }

        user.ReplaceRoles(request.RoleIds, request.ChangedByUserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(
            new UserRolesChangedV1(Guid.NewGuid(), DateTime.UtcNow, user.Id, user.Email, newRoleNames, request.ChangedByUserId),
            cancellationToken);

        return Result.Success();
    }
}
