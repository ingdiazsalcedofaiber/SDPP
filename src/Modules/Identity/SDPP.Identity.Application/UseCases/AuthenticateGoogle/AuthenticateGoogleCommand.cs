using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Application.Services;

namespace SDPP.Identity.Application.UseCases.AuthenticateGoogle;

public sealed record AuthenticateGoogleCommand(
    string IdToken, string? IpAddress, string? UserAgent, bool IsDevelopment) : ICommand<FirstFactorOutcome>;

/// <summary>
/// UC "iniciar sesión con Google": validates the ID token server-side (never trusts the frontend
/// alone, see IGoogleTokenValidator), then delegates the rest — domain policy, upsert, MFA
/// challenge issuance — to LoginCompletionService. This never resolves into a session directly:
/// MFA is mandatory from the first login, so the real session is only issued once the caller
/// completes ConfirmMfaEnrollmentCommand or VerifyMfaCommand.
/// </summary>
public sealed class AuthenticateGoogleHandler(
    IGoogleTokenValidator googleTokenValidator, ILoginCompletionService loginCompletionService)
    : IRequestHandler<AuthenticateGoogleCommand, Result<FirstFactorOutcome>>
{
    public async Task<Result<FirstFactorOutcome>> Handle(AuthenticateGoogleCommand request, CancellationToken cancellationToken)
    {
        var identity = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return Result.Success(FirstFactorOutcome.Failed("INVALID_TOKEN", "El token de Google no es válido o expiró."));
        }

        if (!identity.EmailVerified)
        {
            return Result.Success(FirstFactorOutcome.Failed("EMAIL_NOT_VERIFIED", "Tu cuenta de Google no tiene el correo verificado."));
        }

        var outcome = await loginCompletionService.AuthenticateFirstFactorAsync(
            new LoginContext(
                "Google", identity.Sub, identity.Email, identity.Name, identity.Picture,
                identity.EmailVerified, identity.HostedDomain, request.IpAddress, request.UserAgent, request.IsDevelopment),
            cancellationToken);

        return Result.Success(outcome);
    }
}
