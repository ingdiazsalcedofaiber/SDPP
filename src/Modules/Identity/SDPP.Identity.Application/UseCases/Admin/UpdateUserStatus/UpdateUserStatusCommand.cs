using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Identity;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.Admin.UpdateUserStatus;

public sealed record UpdateUserStatusCommand(Guid UserId, bool Active, Guid ChangedByUserId) : ICommand;

/// <summary>Deactivating a user revokes every active session and blocklists their user id for the
/// remaining lifetime of the longest-lived access token still outstanding (15 min ceiling) —
/// access dies immediately instead of waiting for tokens to expire on their own.</summary>
public sealed class UpdateUserStatusHandler(
    IUserRepository userRepository, ISessionRepository sessionRepository, IUnitOfWork unitOfWork,
    IRevokedTokenStore revokedTokenStore, IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<UpdateUserStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure("El usuario no existe.", "USER_NOT_FOUND");
        }

        if (request.UserId == request.ChangedByUserId && !request.Active)
        {
            return Result.Failure("No puedes desactivar tu propia cuenta.", "SELF_DEACTIVATION_BLOCKED");
        }

        user.SetActive(request.Active);

        if (!request.Active)
        {
            foreach (var session in await sessionRepository.GetActiveByUserIdAsync(user.Id, cancellationToken))
            {
                session.Revoke();
            }
            await revokedTokenStore.RevokeUserAsync(user.Id, TimeSpan.FromMinutes(15), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(
            new UserStatusChangedV1(Guid.NewGuid(), DateTime.UtcNow, user.Id, user.Email, request.Active, request.ChangedByUserId),
            cancellationToken);

        return Result.Success();
    }
}
