using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Application.UseCases.GetDocumentStatus;

public sealed record ConversionJobDto(
    Guid Id, string OperationType, string Status, Guid? OutputDocumentId, string? ErrorDetail,
    string? OutputIntegritySignature, IReadOnlyList<string> OutputProtectionsApplied);

public sealed record DocumentStatusResult(
    Guid Id, string OriginalFileName, string Status, string Classification, IReadOnlyList<ConversionJobDto> Jobs,
    int? RiskScore, string? Category, IReadOnlyList<string> Labels);

public sealed record GetDocumentStatusQuery(Guid DocumentId) : IQuery<DocumentStatusResult>;

/// <summary>
/// A query handler reading through the write-side repository for simplicity in this scaffold.
/// Under load this is exactly the seam docs/01-architecture/technology-stack.md §2 calls out for
/// swapping to a Dapper-backed read model against a replica, without touching the command side.
///
/// Classification/risk/category/labels and every job output's integrity signature/protections no
/// longer live on DocumentInstance (see the "Clasificación de Activos de Información" extraction)
/// — this handler enriches its own response by calling Classification.Api server-side, so the
/// response shape and the frontend that consumes it are completely unchanged.
/// </summary>
public sealed class GetDocumentStatusHandler(IDocumentRepository repository, IClassificationClient classificationClient, ICurrentActor currentActor)
    : IRequestHandler<GetDocumentStatusQuery, Result<DocumentStatusResult>>
{
    public async Task<Result<DocumentStatusResult>> Handle(GetDocumentStatusQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure<DocumentStatusResult>("El documento no existe.", "DOCUMENT_NOT_FOUND");
        }
        if (!DocumentAuthorization.CanView(document, currentActor))
        {
            return Result.Failure<DocumentStatusResult>("El documento no existe.", "DOCUMENT_NOT_FOUND");
        }

        var outputIds = document.Jobs.Where(j => j.OutputDocumentId is not null).Select(j => j.OutputDocumentId!.Value).ToList();
        var integrityByDocumentId = (await classificationClient.GetBatchIntegrityAsync(outputIds, cancellationToken))
            .ToDictionary(i => i.DocumentId);

        var jobs = document.Jobs.Select(job =>
        {
            // The protection details (watermark/footer/signature applied) belong to the *output*
            // document, not the job itself — ProtectionEngine only ever touches conversion
            // outputs (see the protection scope decision in the approved plan), never the source.
            var integrity = job.OutputDocumentId is { } outputId ? integrityByDocumentId.GetValueOrDefault(outputId) : null;

            return new ConversionJobDto(
                job.Id, job.OperationType.ToString(), job.Status.ToString(), job.OutputDocumentId, job.ErrorDetail,
                integrity?.IntegritySignature, integrity?.ProtectionsApplied ?? []);
        }).ToList();

        var summary = await classificationClient.GetVersionSummaryAsync(document.DocumentVersionId, cancellationToken);

        return Result.Success(new DocumentStatusResult(
            document.Id, document.OriginalFileName, document.Status.ToString(), summary.Classification?.ToString() ?? "UsoInterno", jobs,
            summary.RiskScore, summary.Category, summary.Labels));
    }
}
