using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.RefreshSession;

public sealed record RefreshSessionResult(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RawRefreshToken);

/// <summary>
/// Rotates the opaque refresh secret (invalidating the previous one — mitigates replay of a
/// stolen refresh cookie) and mints a fresh 15-minute access token. Never extends the session's
/// absolute 8h lifetime (see Session.Rotate).
/// </summary>
public sealed record RefreshSessionCommand(string RawRefreshToken) : ICommand<RefreshSessionResult>;

public sealed class RefreshSessionHandler(
    ISessionRepository sessionRepository, IUserRepository userRepository, IRoleRepository roleRepository,
    IAccessTokenIssuer accessTokenIssuer, IUnitOfWork unitOfWork)
    : IRequestHandler<RefreshSessionCommand, Result<RefreshSessionResult>>
{
    public async Task<Result<RefreshSessionResult>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var hash = Hash(request.RawRefreshToken);
        var session = await sessionRepository.GetByRefreshTokenHashAsync(hash, cancellationToken);
        if (session is null || !session.IsActive)
        {
            return Result.Failure<RefreshSessionResult>("La sesión no existe o expiró.", "SESSION_INVALID");
        }

        var user = await userRepository.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null || !user.Active)
        {
            session.Revoke();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<RefreshSessionResult>("El usuario ya no tiene acceso.", "ACCOUNT_INACTIVE");
        }

        var allRoles = await roleRepository.GetAllAsync(cancellationToken);
        var roleNames = user.Roles.Select(ur => allRoles.First(r => r.Id == ur.RoleId).Name).ToList();

        var rawRefreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        session.Rotate(Hash(rawRefreshToken));

        var issued = accessTokenIssuer.Issue(user.Id, user.FullName, user.Email, user.Domain, user.Department, roleNames);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RefreshSessionResult(issued.Token, issued.ExpiresAtUtc, rawRefreshToken));
    }

    private static string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
