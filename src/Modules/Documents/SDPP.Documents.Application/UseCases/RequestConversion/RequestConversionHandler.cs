using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Aggregates;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Application.UseCases.RequestConversion;

/// <summary>
/// Implements the Conversion Panel's single responsibility: convert a file. No mandatory
/// business-justification form, no policy gate (block/require-approval), no retention
/// requirement — those were compliance/document-management concerns that used to live on this
/// request; the Panel de Conversión simplification removed them from here entirely (see the
/// approved plan). Automatic classification still runs (via Classification.Api's ClassifyAsync)
/// purely so the Worker's already-existing automatic protection/watermark step has the data it
/// needs — that keeps working invisibly; it's just no longer shown or gated on in this flow.
/// </summary>
public sealed class RequestConversionHandler(
    IDocumentRepository repository,
    IDocumentVersionRepository documentVersionRepository,
    IClassificationClient classificationClient,
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<RequestConversionCommand, Result<RequestConversionResult>>
{
    public async Task<Result<RequestConversionResult>> Handle(RequestConversionCommand request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure<RequestConversionResult>("El documento no existe.", "DOCUMENT_NOT_FOUND");
        }

        var actorSnapshot = new ActorSnapshot(
            currentActor.UserId, currentActor.FullName, currentActor.Email, currentActor.Domain,
            currentActor.IpAddress, Hostname: null, OperatingSystem: null, currentActor.UserAgent, MacAddress: null);

        ClassifyDocumentDecision? classification = null;

        if (document.Status == DocumentStatus.Uploaded)
        {
            document.BeginInspection();

            var documentVersion = await documentVersionRepository.GetByIdAsync(document.DocumentVersionId, cancellationToken)
                ?? throw new InvalidOperationException($"La versión '{document.DocumentVersionId}' del documento no existe.");

            classification = await classificationClient.ClassifyAsync(
                document.Id, documentVersion.Id, documentVersion.LogicalDocumentId, document.ContentType, cancellationToken);

            document.CompleteInspection(classification.RequiresManualReview);

            await integrationEventPublisher.PublishAsync(new DocumentVersionCreatedV1(
                Guid.NewGuid(), DateTime.UtcNow, document.Id, documentVersion.LogicalDocumentId, documentVersion.Id,
                documentVersion.VersionNumber, classification.ChangeType, PreviousContentFingerprint: null,
                classification.ContentFingerprint ?? string.Empty, classification.InheritedFromVersionId, PreviousClassification: null,
                classification.Classification.ToString(), actorSnapshot),
                cancellationToken);
        }

        if (document.Status == DocumentStatus.Blocked)
        {
            return Result.Failure<RequestConversionResult>(
                "El documento está bloqueado tras la inspección automática y requiere revisión manual.", "DOCUMENT_BLOCKED");
        }

        // A conversion request for a document already classified on a *previous* call (Status !=
        // Uploaded at the top of this method) still needs the classification data to hand to the
        // Worker for automatic protection — fetch the summary Classification.Api already computed
        // rather than re-running ClassifyAsync's full orchestration a second time.
        classification ??= await FetchExistingClassificationAsync(document, cancellationToken);

        var job = document.RequestConversion(request.OperationType);
        document.QueueJob(job.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new ConversionRequestedV1(
            Guid.NewGuid(), DateTime.UtcNow, job.Id, document.Id, request.OperationType.ToString(),
            document.StorageLocation.Bucket, document.StorageLocation.ObjectKey, classification.Classification.ToString(),
            request.OperationParameters, actorSnapshot,
            classification.RiskScore, classification.Category, classification.Labels, Area: null),
            cancellationToken);

        return Result.Success(new RequestConversionResult(job.Id, job.Status.ToString()));
    }

    /// <summary>Re-derives the ClassifyDocumentDecision shape from Classification.Api's summary
    /// endpoint for a document that was already classified on a previous RequestConversion call —
    /// change-detection/inheritance never needs to run again for the same DocumentVersionId.</summary>
    private async Task<ClassifyDocumentDecision> FetchExistingClassificationAsync(DocumentInstance document, CancellationToken cancellationToken)
    {
        var summary = await classificationClient.GetVersionSummaryAsync(document.DocumentVersionId, cancellationToken);
        return new ClassifyDocumentDecision(
            summary.Classification ?? ClassificationLevel.UsoInterno, ClassificationSource: "Hybrid", RequiresManualReview: false,
            summary.RiskScore ?? 0, summary.Category, summary.Labels,
            ContentFingerprint: null, StructuralSignature: null, ChangeType: "None", InheritedFromVersionId: null);
    }
}
