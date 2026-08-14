using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Services;

namespace SDPP.Identity.Application.UseCases.Mfa;

public sealed record ConfirmMfaEnrollmentCommand(
    string RawChallengeToken, string Code, string? IpAddress, string? UserAgent) : ICommand<LoginOutcome>;

/// <summary>UC "confirmar activación de MFA": the second half of a first-ever login — validates the
/// TOTP code against the secret generated in AuthenticateFirstFactorAsync and, only on success,
/// issues the real session (see LoginCompletionService.CompleteMfaEnrollmentAsync).</summary>
public sealed class ConfirmMfaEnrollmentHandler(ILoginCompletionService loginCompletionService)
    : IRequestHandler<ConfirmMfaEnrollmentCommand, Result<LoginOutcome>>
{
    public async Task<Result<LoginOutcome>> Handle(ConfirmMfaEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var outcome = await loginCompletionService.CompleteMfaEnrollmentAsync(
            request.RawChallengeToken, request.Code, request.IpAddress, request.UserAgent, cancellationToken);
        return Result.Success(outcome);
    }
}
