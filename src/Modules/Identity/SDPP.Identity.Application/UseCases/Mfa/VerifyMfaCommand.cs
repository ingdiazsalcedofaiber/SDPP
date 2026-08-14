using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Identity.Application.Services;

namespace SDPP.Identity.Application.UseCases.Mfa;

public sealed record VerifyMfaCommand(
    string RawChallengeToken, string Code, string? IpAddress, string? UserAgent) : ICommand<LoginOutcome>;

/// <summary>UC "verificar MFA": the second factor on every login after the first — accepts either a
/// live TOTP code or a single-use backup code, issuing the real session only on success (see
/// LoginCompletionService.VerifyMfaAsync).</summary>
public sealed class VerifyMfaHandler(ILoginCompletionService loginCompletionService)
    : IRequestHandler<VerifyMfaCommand, Result<LoginOutcome>>
{
    public async Task<Result<LoginOutcome>> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var outcome = await loginCompletionService.VerifyMfaAsync(
            request.RawChallengeToken, request.Code, request.IpAddress, request.UserAgent, cancellationToken);
        return Result.Success(outcome);
    }
}
