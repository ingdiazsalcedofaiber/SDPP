using System.Security.Cryptography;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Aggregates;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Application.UseCases.CreateSignedVersion;

/// <summary>
/// Internal, service-to-service endpoint consumed by Signature.Api: registers a signed PDF as a
/// brand-new DocumentVersion (via DocumentVersion.CreateNext — the first real caller of that
/// factory in the codebase) rather than reusing the source's version the way format-only
/// conversion outputs do, because signing is a genuine, bounded content change. The original
/// DocumentInstance/DocumentVersion are never touched — "preservar el original" is automatic here
/// simply by never calling anything that mutates them. Signature.Application is the one that
/// publishes the DocumentSignedV1 integration event (it has the full signing context: actor,
/// page/position, both hashes) — this handler stays silent on the bus, same discipline as
/// UploadOutputAsync in the Worker not publishing anything beyond what its caller does.
/// </summary>
public sealed class CreateSignedVersionHandler(
    IDocumentRepository repository,
    ILogicalDocumentRepository logicalDocumentRepository,
    IDocumentVersionRepository documentVersionRepository,
    IBlobStorage blobStorage,
    IVirusScanner virusScanner,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateSignedVersionCommand, Result<CreateSignedVersionResult>>
{
    public async Task<Result<CreateSignedVersionResult>> Handle(CreateSignedVersionCommand request, CancellationToken cancellationToken)
    {
        var sourceDocument = await repository.GetByIdAsync(request.SourceDocumentId, cancellationToken);
        if (sourceDocument is null)
        {
            return Result.Failure<CreateSignedVersionResult>("El documento original no existe.", "DOCUMENT_NOT_FOUND");
        }

        if (sourceDocument.Status == DocumentStatus.Locked)
        {
            return Result.Failure<CreateSignedVersionResult>(
                "El documento original está bloqueado y no admite nuevas versiones.", "DOCUMENT_LOCKED");
        }

        var sourceVersion = await documentVersionRepository.GetByIdAsync(sourceDocument.DocumentVersionId, cancellationToken)
            ?? throw new InvalidOperationException($"La versión '{sourceDocument.DocumentVersionId}' del documento no existe.");

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"sdpp-signed-version-{Guid.NewGuid():N}.tmp");
        await using var tempFile = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await request.Content.CopyToAsync(tempFile, cancellationToken);

        tempFile.Position = 0;
        var scanResult = await virusScanner.ScanAsync(tempFile, cancellationToken);
        if (!scanResult.IsClean)
        {
            return Result.Failure<CreateSignedVersionResult>(
                $"El documento firmado fue rechazado por el motor antimalware ({scanResult.ThreatName}).", "MALWARE_DETECTED");
        }

        tempFile.Position = 0;
        var hashHex = Convert.ToHexStringLower(await SHA256.HashDataAsync(tempFile, cancellationToken));

        var newVersion = DocumentVersion.CreateNext(sourceVersion, ChangeType.PartialModification, request.CreatedByUserId);

        var logicalDocument = await logicalDocumentRepository.GetByIdAsync(newVersion.LogicalDocumentId, cancellationToken)
            ?? throw new InvalidOperationException($"El documento lógico '{newVersion.LogicalDocumentId}' no existe.");
        logicalDocument.AdvanceCurrentVersion(newVersion.Id);

        var signedDocument = DocumentInstance.Upload(
            sourceDocument.OwnerId, request.OriginalFileName, request.ContentType, request.SizeBytes,
            newVersion.Id, convertedFromInstanceId: sourceDocument.Id);
        signedDocument.CompleteInspection(requiresManualReview: false);

        tempFile.Position = 0;
        await blobStorage.SaveAsync(signedDocument.StorageLocation, tempFile, request.ContentType, cancellationToken);

        documentVersionRepository.Add(newVersion);
        repository.Add(signedDocument);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateSignedVersionResult(signedDocument.Id, newVersion.Id, sourceVersion.Id, hashHex));
    }
}
