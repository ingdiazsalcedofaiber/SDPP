using MediatR;
using Microsoft.AspNetCore.Mvc;
using SDPP.Classification.Application.UseCases.ClassifyDocument;
using SDPP.Classification.Application.UseCases.EvaluatePolicy;
using SDPP.Classification.Application.UseCases.FindDocumentByHash;
using SDPP.Classification.Application.UseCases.GetBatchIntegrity;
using SDPP.Classification.Application.UseCases.GetVersionSummary;
using SDPP.Classification.Application.UseCases.InspectDocument;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Api.Endpoints;

public static class ClassificationEndpoints
{
    public static void MapClassificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/classification").RequireAuthorization().WithTags("Classification");

        group.MapPost("/inspections/{documentId:guid}", InspectAsync)
            .WithName("InspectDocument")
            .Produces<InspectDocumentResult>();

        group.MapPost("/policies/evaluate", EvaluateAsync)
            .WithName("EvaluatePolicy")
            .Produces<EvaluatePolicyResult>();

        // "Clasificación de Activos de Información" extraction — Documents.Api calls these three
        // instead of running fingerprinting/change-detection/inspection orchestration itself.
        group.MapPost("/documents/{documentId:guid}/classify", ClassifyAsync)
            .WithName("ClassifyDocument")
            .Produces<ClassifyDocumentResult>();

        group.MapGet("/versions/{documentVersionId:guid}/summary", GetVersionSummaryAsync)
            .WithName("GetVersionSummary")
            .Produces<VersionSummaryResult>();

        group.MapGet("/documents/batch-integrity", GetBatchIntegrityAsync)
            .WithName("GetBatchIntegrity")
            .Produces<IReadOnlyList<DocumentIntegrityDto>>();

        // Backs the Audit module's "buscar por hash" flow — Audit.Api has no direct knowledge of
        // hashes (they live here, see the "Clasificación de Activos de Información" extraction),
        // so the frontend resolves hash -> DocumentId here first, then queries Audit.Api's
        // existing documentId filter.
        group.MapGet("/documents/by-hash/{hash}", FindDocumentByHashAsync)
            .WithName("FindDocumentByHash")
            .Produces<FindDocumentByHashResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> FindDocumentByHashAsync(
        [FromRoute] string hash, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new FindDocumentByHashQuery(hash), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new ProblemDetails { Title = result.Error, Detail = result.ErrorCode });
    }

    public sealed record ClassifyBody(Guid DocumentVersionId, Guid LogicalDocumentId, string ContentType);

    private static async Task<IResult> ClassifyAsync(
        [FromRoute] Guid documentId, [FromBody] ClassifyBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ClassifyDocumentCommand(documentId, body.DocumentVersionId, body.LogicalDocumentId, body.ContentType), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error);
    }

    private static async Task<IResult> GetVersionSummaryAsync(
        [FromRoute] Guid documentVersionId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVersionSummaryQuery(documentVersionId), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetBatchIntegrityAsync(
        [FromQuery] Guid[] ids, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBatchIntegrityQuery(ids), cancellationToken);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> InspectAsync([FromRoute] Guid documentId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InspectDocumentCommand(documentId, InspectionTrigger.PreConversion), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error);
    }

    public sealed record EvaluatePolicyBody(ClassificationLevel Classification, string OperationType, string Area, string? Category = null);

    private static async Task<IResult> EvaluateAsync([FromBody] EvaluatePolicyBody body, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new EvaluatePolicyQuery(body.Classification, body.OperationType, body.Area, body.Category), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error);
    }
}
