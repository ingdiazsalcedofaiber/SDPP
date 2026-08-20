using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Documents.Application.UseCases.CreateSignedVersion;
using SDPP.Documents.Application.UseCases.DownloadDocument;
using SDPP.Documents.Application.UseCases.GetDocumentStatus;
using SDPP.Documents.Application.UseCases.GetExtractedText;
using SDPP.Documents.Application.UseCases.LockDocument;
using SDPP.Documents.Application.UseCases.UploadDocument;

namespace SDPP.Documents.Api.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/documents").RequireAuthorization().WithTags("Documents");

        group.MapPost("/", UploadAsync)
            .DisableAntiforgery()
            .WithName("UploadDocument")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadDocumentResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{documentId:guid}", GetStatusAsync)
            .WithName("GetDocumentStatus")
            .Produces<DocumentStatusResult>();

        // AllowAnonymous + InternalServiceKeyFilter (not the group's default RequireAuthorization):
        // still requires a valid SDPP session for a normal browser download, but also accepts the
        // shared internal-service key so Signature.Api can proxy this on behalf of an external
        // envelope recipient who has no SDPP session at all. See InternalServiceKeyFilter.
        group.MapGet("/{documentId:guid}/download", DownloadAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyFilter>()
            .WithName("DownloadDocument");

        // Internal, service-to-service endpoint consumed by Classification API — see
        // docs/01-architecture/c4-diagrams.md and SDPP.Classification.Infrastructure.DocumentContent.
        // InternalServiceKeyOnlyFilter (not just RequireAuthorization): no browser client has any
        // legitimate reason to call this directly, so unlike /download it never accepts a bare user
        // session — see InternalServiceKeyOnlyFilter's doc comment.
        group.MapGet("/{documentId:guid}/extracted-text", GetExtractedTextAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyOnlyFilter>()
            .WithName("GetExtractedText")
            .Produces<ExtractedTextResult>();

        // Internal, service-to-service endpoint consumed by Signature.Api: registers a signed PDF
        // as a brand-new DocumentVersion linked to documentId via ConvertedFromInstanceId — the
        // original document/version are never modified by this call. See
        // SDPP.Documents.Application.UseCases.CreateSignedVersion.CreateSignedVersionHandler.
        // InternalServiceKeyOnlyFilter, not InternalServiceKeyFilter: this must never be reachable
        // by a bare authenticated session (any logged-in user could otherwise inject a forged
        // "signed version" — createdByUserId here is trusted precisely because only the Signature
        // module, which always attaches the internal key, can reach this at all).
        group.MapPost("/{documentId:guid}/signed-version", CreateSignedVersionAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyOnlyFilter>()
            .DisableAntiforgery()
            .WithName("CreateSignedVersion")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<CreateSignedVersionResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // Internal, service-to-service endpoint consumed by Signature.Api right after it produces
        // the final signed artifact of a completed envelope. Irreversible — see DocumentInstance.Lock.
        // InternalServiceKeyOnlyFilter: a bare authenticated session must never be able to lock an
        // arbitrary document it doesn't own (see InternalServiceKeyOnlyFilter's doc comment).
        group.MapPost("/{documentId:guid}/lock", LockAsync)
            .AllowAnonymous()
            .AddEndpointFilter<InternalServiceKeyOnlyFilter>()
            .WithName("LockDocument")
            .Produces<LockDocumentResult>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> LockAsync(
        [FromRoute] Guid documentId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LockDocumentCommand(documentId), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> CreateSignedVersionAsync(
        [FromRoute] Guid documentId, IFormFile file, [FromForm] Guid createdByUserId, ISender sender, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var command = new CreateSignedVersionCommand(stream, file.FileName, file.ContentType, file.Length, documentId, createdByUserId);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/documents/{result.Value.DocumentId}", result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> GetExtractedTextAsync(
        [FromRoute] Guid documentId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetExtractedTextQuery(documentId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new ProblemDetails { Title = result.Error });
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file, ISender sender, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var command = new UploadDocumentCommand(stream, file.FileName, file.ContentType, file.Length);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/documents/{result.Value.DocumentId}", result.Value)
            : Results.UnprocessableEntity(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    private static async Task<IResult> DownloadAsync(
        [FromRoute] Guid documentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DownloadDocumentQuery(documentId), cancellationToken);
        if (!result.IsSuccess)
        {
            return Results.NotFound(new ProblemDetails { Title = result.Error });
        }

        // Consumed by Signature.Api's HttpDocumentsClient — it needs the source DocumentVersionId
        // to seed a new envelope without a second round-trip.
        httpContext.Response.Headers["X-SDPP-Document-Version-Id"] = result.Value.DocumentVersionId.ToString();
        return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private static async Task<IResult> GetStatusAsync(
        [FromRoute] Guid documentId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetDocumentStatusQuery(documentId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(new ProblemDetails { Title = result.Error });
    }
}
