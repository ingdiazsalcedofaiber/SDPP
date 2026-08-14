using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Signature.Application.UseCases.SignerAccess;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Api.Endpoints;

/// <summary>The public, recipient-facing side of the envelope lifecycle — every endpoint here is
/// AllowAnonymous + protected instead by the per-recipient token/OTP/session mechanism (see
/// SignerAccessChallenge and RecipientAccessAuthorization). Reached by both external recipients
/// (no SDPP account at all) and internal ones (who additionally need a valid SDPP session for their
/// own matched account — the JwtBearer middleware still runs and populates ICurrentActor normally
/// even on an AllowAnonymous endpoint if a valid session cookie is present).</summary>
public static class SignerAccessEndpoints
{
    public static void MapSignerAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/signature/access").AllowAnonymous().WithTags("SignerAccess");

        group.MapGet("/{token}", ViewAsync).WithName("ViewEnvelopeAccess").Produces<EnvelopeAccessView>();
        group.MapGet("/{token}/document", DownloadDocumentAsync).WithName("DownloadEnvelopeDocument");
        group.MapPost("/{token}/otp/request", RequestOtpAsync).WithName("RequestOtp").Produces<RequestOtpResult>();
        group.MapPost("/{token}/otp/verify", VerifyOtpAsync).WithName("VerifyOtp").Produces<VerifyOtpResult>();
        group.MapPost("/{token}/consent", ConsentAsync).WithName("RegisterConsent");
        group.MapPost("/{token}/complete", CompleteAsync).WithName("CompleteRecipientSigning").Produces<CompleteRecipientSigningResult>();
        group.MapPost("/{token}/decline", DeclineAsync).WithName("DeclineEnvelope");
    }

    private sealed record VerifyOtpBody(string Code);
    private sealed record DeclineBody(string Reason);
    private sealed record FieldSubmissionBody(Guid FieldId, string? Value, string? SignatureImageBase64, SignatureMethodUsed? Method);
    private sealed record CompleteBody(IReadOnlyList<FieldSubmissionBody> Fields);

    private static string? ExtractBearerToken(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? header[prefix.Length..] : null;
    }

    private static async Task<IResult> ViewAsync([FromRoute] string token, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ViewEnvelopeAccessCommand(token), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> DownloadDocumentAsync([FromRoute] string token, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DownloadEnvelopeDocumentQuery(token), cancellationToken);
        return result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> RequestOtpAsync([FromRoute] string token, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RequestOtpCommand(token), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> VerifyOtpAsync(
        [FromRoute] string token, [FromBody] VerifyOtpBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new VerifyOtpCommand(token, body.Code), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Json(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> ConsentAsync(
        [FromRoute] string token, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        var sessionToken = ExtractBearerToken(httpContext);
        var result = await sender.Send(new RegisterConsentCommand(token, sessionToken), cancellationToken);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.Json(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> CompleteAsync(
        [FromRoute] string token, [FromBody] CompleteBody body, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        var sessionToken = ExtractBearerToken(httpContext);
        var fields = body.Fields
            .Select(f => new FieldSubmission(f.FieldId, f.Value, f.SignatureImageBase64 is null ? null : Convert.FromBase64String(f.SignatureImageBase64), f.Method))
            .ToList();

        var result = await sender.Send(new CompleteRecipientSigningCommand(token, sessionToken, fields), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Json(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> DeclineAsync(
        [FromRoute] string token, [FromBody] DeclineBody body, HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        var sessionToken = ExtractBearerToken(httpContext);
        var result = await sender.Send(new DeclineEnvelopeCommand(token, sessionToken, body.Reason), cancellationToken);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.Json(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
