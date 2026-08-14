using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Classification.Application.Ports;
using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;
using SDPP.Classification.Application.UseCases.InspectDocument;

namespace SDPP.Classification.Application.UseCases.ClassifyDocument;

public sealed record ClassifyDocumentResult(
    string Classification, string ClassificationSource, bool RequiresManualReview,
    int RiskScore, string? Category, IReadOnlyList<string> Labels,
    string? ContentFingerprint, string? StructuralSignature, string ChangeType, Guid? InheritedFromVersionId);

public sealed record ClassifyDocumentCommand(
    Guid DocumentId, Guid DocumentVersionId, Guid LogicalDocumentId, string ContentType) : ICommand<ClassifyDocumentResult>;

/// <summary>
/// Replaces the fingerprint/change-detection/inspect orchestration that used to live in
/// Documents.Application.RequestConversionHandler — see the "Clasificación de Activos de
/// Información" extraction. Fingerprints the document's extracted text and either inherits an
/// existing DocumentVersionFingerprint's classification (fingerprint already seen, from anywhere
/// on the platform — see IDocumentVersionFingerprintRepository) or falls through to a real
/// inspection (reusing InspectDocumentHandler's pipeline unchanged, via a nested MediatR dispatch)
/// for genuinely new content.
/// </summary>
public sealed class ClassifyDocumentHandler(
    IDocumentContentClient contentClient,
    IContentFingerprintService fingerprintService,
    IChangeDetectionService changeDetectionService,
    IDocumentVersionFingerprintRepository fingerprintRepository,
    IDocumentIntegrityRecordRepository integrityRepository,
    ISender sender,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ClassifyDocumentCommand, Result<ClassifyDocumentResult>>
{
    public async Task<Result<ClassifyDocumentResult>> Handle(ClassifyDocumentCommand request, CancellationToken cancellationToken)
    {
        var existingFingerprint = await fingerprintRepository.GetByDocumentVersionIdAsync(request.DocumentVersionId, cancellationToken);
        var fingerprintRecord = existingFingerprint ?? DocumentVersionFingerprint.Create(request.DocumentVersionId, request.LogicalDocumentId);
        var isNewFingerprintRecord = existingFingerprint is null;

        var myIntegrity = await integrityRepository.GetByDocumentIdAsync(request.DocumentId, cancellationToken);

        var content = await contentClient.GetContentForInspectionAsync(request.DocumentId, cancellationToken);
        var fingerprint = fingerprintService.Compute(content.ExtractedText);

        var matchedVersion = await fingerprintRepository.GetLatestByContentFingerprintAsync(fingerprint.ContentFingerprint, cancellationToken);

        string changeType;
        Guid? inheritedFromVersionId = null;
        bool requiresManualReview;

        if (matchedVersion is not null)
        {
            var matchedIntegrity = await integrityRepository.GetLatestByDocumentVersionIdAsync(matchedVersion.DocumentVersionId, cancellationToken);

            var detected = changeDetectionService.Detect(new ChangeDetectionInput(
                myIntegrity?.Sha256Hash.Value, request.ContentType, fingerprint.ContentFingerprint, fingerprint.StructuralSignature,
                matchedVersion, matchedIntegrity?.Sha256Hash.Value, matchedIntegrity?.ContentType));

            fingerprintRecord.SetFingerprint(fingerprint.ContentFingerprint, fingerprint.StructuralSignature);
            fingerprintRecord.InheritClassificationFrom(matchedVersion);
            changeType = detected.ToString();
            inheritedFromVersionId = matchedVersion.DocumentVersionId;
            requiresManualReview = false;
        }
        else
        {
            var inspectionResult = await sender.Send(new InspectDocumentCommand(request.DocumentId, InspectionTrigger.PreConversion), cancellationToken);
            if (!inspectionResult.IsSuccess)
            {
                return Result.Failure<ClassifyDocumentResult>(inspectionResult.Error!, inspectionResult.ErrorCode ?? "CLASSIFICATION_FAILED");
            }

            var inspection = inspectionResult.Value;
            fingerprintRecord.SetFingerprint(fingerprint.ContentFingerprint, fingerprint.StructuralSignature);
            fingerprintRecord.ApplyClassification(
                Enum.Parse<ClassificationLevel>(inspection.SuggestedClassification), ClassificationSource.Hybrid,
                inspection.RiskScore, inspection.BusinessCategory, inspection.Labels);
            changeType = ChangeType.Initial.ToString();
            requiresManualReview = inspection.RequiresManualReview;
        }

        if (isNewFingerprintRecord)
        {
            fingerprintRepository.Add(fingerprintRecord);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ClassifyDocumentResult(
            fingerprintRecord.Classification.ToString(), fingerprintRecord.ClassificationSource.ToString(), requiresManualReview,
            fingerprintRecord.RiskScore ?? 0, fingerprintRecord.Category, fingerprintRecord.Labels,
            fingerprintRecord.ContentFingerprint, fingerprintRecord.StructuralSignature, changeType, inheritedFromVersionId));
    }
}
