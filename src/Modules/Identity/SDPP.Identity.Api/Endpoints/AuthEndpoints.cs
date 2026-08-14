using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Identity.Api.Security;
using SDPP.Identity.Application.Services;
using SDPP.Identity.Application.UseCases.Admin.ListAccessHistory;
using SDPP.Identity.Application.UseCases.AuthenticateGoogle;
using SDPP.Identity.Application.UseCases.GetCurrentUser;
using SDPP.Identity.Application.UseCases.Logout;
using SDPP.Identity.Application.UseCases.Mfa;
using SDPP.Identity.Application.UseCases.RefreshSession;

namespace SDPP.Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record GoogleLoginBody(string IdToken);
    public sealed record MfaCodeBody(string Code);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/google", GoogleLoginAsync).AllowAnonymous().SkipCsrf().WithName("AuthenticateWithGoogle");
        group.MapPost("/mfa/enroll/confirm", ConfirmMfaEnrollmentAsync).AllowAnonymous().SkipCsrf().WithName("ConfirmMfaEnrollment");
        group.MapPost("/mfa/verify", VerifyMfaAsync).AllowAnonymous().SkipCsrf().WithName("VerifyMfa");
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous().WithName("RefreshSession");
        group.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("Logout");
        group.MapGet("/me", MeAsync).RequireAuthorization().WithName("GetCurrentUser");
        group.MapGet("/me/access-history", MyAccessHistoryAsync).RequireAuthorization().WithName("GetMyAccessHistory");
    }

    /// <summary>Never resolves into a session directly — MFA is mandatory from the first login, so
    /// this always ends in an enrollment/challenge cookie or a failure (see FirstFactorOutcome).</summary>
    private static async Task<IResult> GoogleLoginAsync(
        GoogleLoginBody body, ISender sender, HttpContext httpContext, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var command = new AuthenticateGoogleCommand(
            body.IdToken, httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString(), env.IsDevelopment());
        var outcome = (await sender.Send(command, cancellationToken)).Value;

        if (!outcome.Success)
        {
            return Results.Json(new { title = outcome.ErrorMessage, errorCode = outcome.ErrorCode }, statusCode: StatusCodes.Status403Forbidden);
        }

        CookieWriter.IssueMfaChallengeCookie(httpContext, env, outcome.RawChallengeToken!, outcome.ChallengeExpiresAtUtc!.Value);

        return outcome.RequiresEnrollment
            ? Results.Ok(new { status = "MFA_ENROLLMENT_REQUIRED", qrCodeDataUri = outcome.QrCodeDataUri, manualEntryKey = outcome.ManualEntryKey })
            : Results.Ok(new { status = "MFA_CHALLENGE_REQUIRED" });
    }

    private static async Task<IResult> ConfirmMfaEnrollmentAsync(
        MfaCodeBody body, HttpContext httpContext, ISender sender, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var rawChallengeToken = httpContext.Request.Cookies[CookieWriter.MfaChallengeCookie];
        if (string.IsNullOrEmpty(rawChallengeToken))
        {
            return Results.Unauthorized();
        }

        var command = new ConfirmMfaEnrollmentCommand(
            rawChallengeToken, body.Code, httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString());
        var outcome = (await sender.Send(command, cancellationToken)).Value;

        if (!outcome.Success)
        {
            return Results.Json(new { title = outcome.ErrorMessage, errorCode = outcome.ErrorCode }, statusCode: StatusCodes.Status401Unauthorized);
        }

        CookieWriter.ClearMfaChallengeCookie(httpContext);
        CookieWriter.IssueSessionCookies(httpContext, env, outcome.AccessToken!, outcome.AccessTokenExpiresAtUtc!.Value, outcome.RawRefreshToken!);
        return Results.Ok(new { user = outcome.User, backupCodes = outcome.BackupCodes });
    }

    private static async Task<IResult> VerifyMfaAsync(
        MfaCodeBody body, HttpContext httpContext, ISender sender, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var rawChallengeToken = httpContext.Request.Cookies[CookieWriter.MfaChallengeCookie];
        if (string.IsNullOrEmpty(rawChallengeToken))
        {
            return Results.Unauthorized();
        }

        var command = new VerifyMfaCommand(
            rawChallengeToken, body.Code, httpContext.Connection.RemoteIpAddress?.ToString(), httpContext.Request.Headers.UserAgent.ToString());
        var outcome = (await sender.Send(command, cancellationToken)).Value;

        if (!outcome.Success)
        {
            return Results.Json(new { title = outcome.ErrorMessage, errorCode = outcome.ErrorCode }, statusCode: StatusCodes.Status401Unauthorized);
        }

        CookieWriter.ClearMfaChallengeCookie(httpContext);
        CookieWriter.IssueSessionCookies(httpContext, env, outcome.AccessToken!, outcome.AccessTokenExpiresAtUtc!.Value, outcome.RawRefreshToken!);
        return Results.Ok(new { user = outcome.User });
    }

    private static async Task<IResult> RefreshAsync(HttpContext httpContext, ISender sender, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var rawRefreshToken = httpContext.Request.Cookies[CookieWriter.RefreshTokenCookie];
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RefreshSessionCommand(rawRefreshToken), cancellationToken);
        if (!result.IsSuccess)
        {
            CookieWriter.ClearSessionCookies(httpContext);
            return Results.Json(new { title = result.Error }, statusCode: StatusCodes.Status401Unauthorized);
        }

        CookieWriter.IssueSessionCookies(httpContext, env, result.Value.AccessToken, result.Value.AccessTokenExpiresAtUtc, result.Value.RawRefreshToken);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        var rawRefreshToken = httpContext.Request.Cookies[CookieWriter.RefreshTokenCookie] ?? string.Empty;
        var jti = httpContext.User.FindFirst("jti")?.Value;
        DateTime? expiresAtUtc = null;
        if (httpContext.User.FindFirst("exp")?.Value is { } expClaim && long.TryParse(expClaim, out var unixSeconds))
        {
            expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }

        await sender.Send(new LogoutCommand(rawRefreshToken, jti, expiresAtUtc), cancellationToken);
        CookieWriter.ClearSessionCookies(httpContext);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCurrentUserQuery(currentActor.UserId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new { title = result.Error });
    }

    /// <summary>Same query/repository the admin panel uses (ListAccessHistoryQuery), but UserId is
    /// always forced to the caller's own id — never accepted from the query string — so a regular
    /// user can never pull another user's access history through this endpoint.</summary>
    private static async Task<IResult> MyAccessHistoryAsync(
        [AsParameters] MyAccessHistoryParams query, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListAccessHistoryQuery(
            currentActor.UserId, null, null, null, null, 1, query.Limit ?? 10), cancellationToken);
        return Results.Ok(result.Value);
    }

    public sealed class MyAccessHistoryParams
    {
        [FromQuery(Name = "limit")] public int? Limit { get; init; }
    }
}
