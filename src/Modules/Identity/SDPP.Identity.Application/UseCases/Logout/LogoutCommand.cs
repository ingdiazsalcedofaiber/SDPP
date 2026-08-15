using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Identity;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.Logout;

/// <summary>Revokes the session server-side (Sessions.RevokedAtUtc) and blocklists the current
/// access token's jti in Redis via IRevokedTokenStore — the combination is what makes this a real
/// logout instead of just "the frontend forgot the cookie."</summary>
public sealed record LogoutCommand(string RawRefreshToken, string? AccessTokenJti, DateTime? AccessTokenExpiresAtUtc) : ICommand;

public sealed class LogoutHandler(
    ISessionRepository sessionRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IRevokedTokenStore revokedTokenStore,
    ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawRefreshToken)));
        var session = await sessionRepository.GetByRefreshTokenHashAsync(hash, cancellationToken);

        if (session is not null)
        {
            session.Revoke();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var user = await userRepository.GetByIdAsync(session.UserId, cancellationToken);
            await integrationEventPublisher.PublishAsync(new SessionLoggedOutV1(
                Guid.NewGuid(), DateTime.UtcNow, session.UserId, user?.Email ?? currentActor.Email ?? string.Empty,
                currentActor.IpAddress, currentActor.UserAgent), cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.AccessTokenJti) && request.AccessTokenExpiresAtUtc is { } expiresAtUtc)
        {
            var ttl = expiresAtUtc - DateTime.UtcNow;
            if (ttl > TimeSpan.Zero)
            {
                await revokedTokenStore.RevokeTokenAsync(request.AccessTokenJti, ttl, cancellationToken);
            }
        }

        return Result.Success();
    }
}
