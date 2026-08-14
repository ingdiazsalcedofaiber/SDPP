using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Signature.Application.UseCases.CancelEnvelope;
using SDPP.Signature.Application.UseCases.ConfigureEnvelope;
using SDPP.Signature.Application.UseCases.CreateEnvelope;
using SDPP.Signature.Application.UseCases.ExportEvidence;
using SDPP.Signature.Application.UseCases.GetEnvelope;
using SDPP.Signature.Application.UseCases.ListEnvelopes;
using SDPP.Signature.Application.UseCases.ResendRecipientAccess;
using SDPP.Signature.Application.UseCases.SendEnvelope;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Api.Endpoints;

/// <summary>The creator-facing side of the envelope lifecycle — everything here requires a normal
/// SDPP session. See SignerAccessEndpoints for the public, recipient-facing side.</summary>
public static class EnvelopeEndpoints
{
    public static void MapEnvelopeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/signature/envelopes").RequireAuthorization().WithTags("Envelopes");

        group.MapPost("/", CreateAsync).WithName("CreateEnvelope").Produces<CreateEnvelopeResult>(StatusCodes.Status201Created);
        group.MapGet("/", ListAsync).WithName("ListEnvelopes").Produces<IReadOnlyList<EnvelopeSummaryDto>>();
        group.MapGet("/{envelopeId:guid}", GetAsync).WithName("GetEnvelope").Produces<EnvelopeDetailDto>();
        group.MapGet("/{envelopeId:guid}/certificate", GetCertificateAsync).WithName("GetEnvelopeCertificate");
        group.MapGet("/{envelopeId:guid}/evidence-package", GetEvidencePackageAsync).WithName("GetEvidencePackage");

        group.MapPost("/{envelopeId:guid}/recipients", AddRecipientAsync).WithName("AddRecipient").Produces<AddRecipientResult>(StatusCodes.Status201Created);
        group.MapDelete("/{envelopeId:guid}/recipients/{recipientId:guid}", RemoveRecipientAsync).WithName("RemoveRecipient");

        group.MapPost("/{envelopeId:guid}/fields", AddFieldAsync).WithName("AddField").Produces<AddFieldResult>(StatusCodes.Status201Created);
        group.MapPatch("/{envelopeId:guid}/fields/{fieldId:guid}", UpdateFieldAsync).WithName("UpdateField");
        group.MapDelete("/{envelopeId:guid}/fields/{fieldId:guid}", RemoveFieldAsync).WithName("RemoveField");

        group.MapPost("/{envelopeId:guid}/send", SendAsync).WithName("SendEnvelope").Produces<SendEnvelopeResult>();
        group.MapPost("/{envelopeId:guid}/cancel", CancelAsync).WithName("CancelEnvelope");

        // Phase 1 has no email delivery yet (see the module redesign plan's Phase 3) — this is the
        // creator's only way to (re)share a recipient's link manually today; once real email exists
        // it doubles as the "reenviar recordatorio" action from the spec.
        group.MapPost("/{envelopeId:guid}/recipients/{recipientId:guid}/resend-access", ResendAccessAsync)
            .WithName("ResendRecipientAccess").Produces<ResendRecipientAccessResult>();
    }

    private sealed record CreateEnvelopeBody(Guid SourceDocumentId, string Title, string? Message, SigningMode SigningMode, DateTime? DueDateUtc);
    private sealed record AddRecipientBody(string Email, string FullName, int Order);
    private sealed record AddFieldBody(Guid RecipientId, FieldType Type, int PageNumber, double PositionX, double PositionY, double Width, double Height, bool Required);
    private sealed record UpdateFieldBody(double PositionX, double PositionY, double Width, double Height);

    private static async Task<IResult> CreateAsync([FromBody] CreateEnvelopeBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateEnvelopeCommand(body.SourceDocumentId, body.Title, body.Message, body.SigningMode, body.DueDateUtc), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/v1/signature/envelopes/{result.Value.EnvelopeId}", result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> ListAsync([FromQuery] string? scope, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListEnvelopesQuery(scope ?? "all"), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetAsync([FromRoute] Guid envelopeId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEnvelopeQuery(envelopeId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new ProblemDetails { Title = result.Error });
    }

    private static async Task<IResult> GetCertificateAsync([FromRoute] Guid envelopeId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEnvelopeCertificateQuery(envelopeId), cancellationToken);
        return result.IsSuccess
            ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName)
            : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> GetEvidencePackageAsync([FromRoute] Guid envelopeId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ExportEvidencePackageQuery(envelopeId), cancellationToken);
        return result.IsSuccess
            ? Results.File(result.Value.Content, "application/zip", result.Value.FileName)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> AddRecipientAsync(
        [FromRoute] Guid envelopeId, [FromBody] AddRecipientBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddRecipientCommand(envelopeId, body.Email, body.FullName, body.Order), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/v1/signature/envelopes/{envelopeId}", result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> RemoveRecipientAsync(
        [FromRoute] Guid envelopeId, [FromRoute] Guid recipientId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveRecipientCommand(envelopeId, recipientId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> AddFieldAsync(
        [FromRoute] Guid envelopeId, [FromBody] AddFieldBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddFieldCommand(
            envelopeId, body.RecipientId, body.Type, body.PageNumber, body.PositionX, body.PositionY, body.Width, body.Height, body.Required),
            cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/v1/signature/envelopes/{envelopeId}", result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> UpdateFieldAsync(
        [FromRoute] Guid envelopeId, [FromRoute] Guid fieldId, [FromBody] UpdateFieldBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateFieldCommand(envelopeId, fieldId, body.PositionX, body.PositionY, body.Width, body.Height), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> RemoveFieldAsync(
        [FromRoute] Guid envelopeId, [FromRoute] Guid fieldId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveFieldCommand(envelopeId, fieldId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> SendAsync([FromRoute] Guid envelopeId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendEnvelopeCommand(envelopeId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> CancelAsync([FromRoute] Guid envelopeId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelEnvelopeCommand(envelopeId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> ResendAccessAsync(
        [FromRoute] Guid envelopeId, [FromRoute] Guid recipientId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResendRecipientAccessCommand(envelopeId, recipientId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }
}
