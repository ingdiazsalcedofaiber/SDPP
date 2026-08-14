using System.Security.Cryptography;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Application.UseCases.UploadDocument;

/// <summary>
/// Implements UC-01 step 2 (docs/04-use-cases/use-cases.md): buffer to a seekable temp file so
/// the hash can be computed server-side before trusting the content, scan for malware, persist
/// metadata, then upload the blob. Order matters — nothing reaches storage before it passes the
/// antimalware gate (fail closed). Publishes DocumentUploadedV1 explicitly (rather than relying
/// on the generic outbox/domain-event passthrough) because only this handler has access to the
/// request-scoped ActorSnapshot (IP, hostname, user agent...) that the integration event needs
/// for traceability — see IIntegrationEventPublisher and docs/05-security/audit-and-traceability.md §1.
///
/// The SHA-256 hash is still computed right here, inline, over bytes already in hand — see the
/// "Clasificación de Activos de Información" extraction: Classification now owns hash *storage*
/// and everything downstream of it (comparisons, inheritance, traceability), populated
/// asynchronously from DocumentUploadedV1 below, but re-fetching these same bytes over HTTP just
/// to have Classification compute the digest itself would double the upload's network cost for
/// no real gain — hashing bytes you already hold is a library call, not a business decision.
/// </summary>
public sealed class UploadDocumentHandler(
    IDocumentRepository repository,
    ILogicalDocumentRepository logicalDocumentRepository,
    IDocumentVersionRepository documentVersionRepository,
    IBlobStorage blobStorage,
    IVirusScanner virusScanner,
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<UploadDocumentCommand, Result<UploadDocumentResult>>
{
    public async Task<Result<UploadDocumentResult>> Handle(UploadDocumentCommand request, CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"sdpp-upload-{Guid.NewGuid():N}.tmp");
        await using (var tempFile = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous))
        {
            await request.Content.CopyToAsync(tempFile, cancellationToken);

            tempFile.Position = 0;
            var scanResult = await virusScanner.ScanAsync(tempFile, cancellationToken);
            if (!scanResult.IsClean)
            {
                return Result.Failure<UploadDocumentResult>(
                    $"El archivo fue rechazado por el motor antimalware ({scanResult.ThreatName}).", "MALWARE_DETECTED");
            }

            tempFile.Position = 0;
            var hashHex = Convert.ToHexStringLower(await SHA256.HashDataAsync(tempFile, cancellationToken));

            // A brand-new upload always establishes a brand-new logical identity — linking it to
            // an existing LogicalDocument (because its fingerprint matches one) only ever happens
            // later, at the first RequestConversion, once real extracted text is available to
            // compute that fingerprint from (see ChangeDetectionService). The fingerprint itself
            // stays null on this initial version until then — same lazy-classification timing the
            // platform already used before this change.
            var logicalDocument = LogicalDocument.Create(currentActor.UserId);
            var documentVersion = DocumentVersion.CreateInitial(logicalDocument.Id, currentActor.UserId);
            logicalDocument.AdvanceCurrentVersion(documentVersion.Id);

            var document = DocumentInstance.Upload(
                currentActor.UserId, request.OriginalFileName, request.DeclaredContentType, request.SizeBytes,
                documentVersion.Id);

            tempFile.Position = 0;
            await blobStorage.SaveAsync(document.StorageLocation, tempFile, request.DeclaredContentType, cancellationToken);

            logicalDocumentRepository.Add(logicalDocument);
            documentVersionRepository.Add(documentVersion);
            repository.Add(document);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await integrationEventPublisher.PublishAsync(new DocumentUploadedV1(
                Guid.NewGuid(), DateTime.UtcNow, document.Id, currentActor.UserId,
                request.OriginalFileName, request.DeclaredContentType, request.SizeBytes, hashHex,
                new ActorSnapshot(
                    currentActor.UserId, currentActor.FullName, currentActor.Email, currentActor.Domain,
                    currentActor.IpAddress, Hostname: null, OperatingSystem: null, currentActor.UserAgent, MacAddress: null),
                documentVersion.Id),
                cancellationToken);

            return Result.Success(new UploadDocumentResult(document.Id, hashHex, document.Status.ToString()));
        }
    }
}
