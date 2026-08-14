using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Signature.Application.UseCases.VerifyEnvelope;

namespace SDPP.Signature.Api.Endpoints;

/// <summary>The one truly public endpoint in this module — no recipient token, no OTP, no SDPP
/// session at all, reachable by anyone who scans the QR code printed on a completion certificate.
/// See VerifyEnvelopeQuery's doc comment for exactly what it does and doesn't expose.</summary>
public static class VerificationEndpoints
{
    public static void MapVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/signature/verify/{envelopeId:guid}", VerifyAsync)
            .AllowAnonymous()
            .WithName("VerifyEnvelope")
            .WithTags("Verification")
            .Produces<EnvelopeVerificationResult>();
    }

    private static async Task<IResult> VerifyAsync([FromRoute] Guid envelopeId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new VerifyEnvelopeQuery(envelopeId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }
}
