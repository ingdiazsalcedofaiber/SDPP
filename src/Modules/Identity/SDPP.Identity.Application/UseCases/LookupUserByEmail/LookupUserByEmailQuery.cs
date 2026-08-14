using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Application.UseCases.LookupUserByEmail;

public sealed record UserLookupResult(Guid UserId, string FullName);

/// <summary>Backs the internal, service-to-service GET /api/v1/identity/users/lookup — consumed by
/// Signature.Api's SendEnvelope handler to resolve whether a recipient's email belongs to an
/// existing SDPP account, before deciding between the internal-session flow and the external
/// magic-link+OTP flow. Not exposed to end users.</summary>
public sealed record LookupUserByEmailQuery(string Email) : IQuery<UserLookupResult>;

public sealed class LookupUserByEmailHandler(IUserRepository userRepository)
    : IRequestHandler<LookupUserByEmailQuery, Result<UserLookupResult>>
{
    public async Task<Result<UserLookupResult>> Handle(LookupUserByEmailQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        return user is null
            ? Result.Failure<UserLookupResult>("No existe un usuario con ese correo.", "USER_NOT_FOUND")
            : Result.Success(new UserLookupResult(user.Id, user.FullName));
    }
}
