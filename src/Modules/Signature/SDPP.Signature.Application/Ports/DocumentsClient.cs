namespace SDPP.Signature.Application.Ports;

public sealed record DownloadedDocument(string FileName, string ContentType, Guid DocumentVersionId, Stream Content);

public sealed record SignedVersionResult(Guid DocumentId, Guid DocumentVersionId, Guid SourceDocumentVersionId, string Sha256Hash);

/// <summary>
/// Outbound HTTP port to Documents.Api — Signature never touches SDPP_Documents directly (it's a
/// separate service with its own database), so downloading the original bytes, registering the
/// final signed output as a new DocumentVersion, and locking that final artifact all go through
/// Documents.Api's own endpoints. Mirrors IClassificationClient's shape/registration pattern
/// (Documents.Infrastructure/Classification).
/// </summary>
public interface IDocumentsClient
{
    Task<DownloadedDocument> DownloadAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Calls Documents.Api's POST /{documentId}/signed-version — creates a brand-new
    /// DocumentVersion (via DocumentVersion.CreateNext) and DocumentInstance for the final,
    /// fully-embedded PDF, preserving the original document/version untouched. createdByUserId is
    /// always the envelope's creator — this call can happen with no SDPP session behind it at all
    /// (an external recipient's completion, relayed via the internal service key), so it can never
    /// rely on Documents.Api resolving an actor from its own ICurrentActor.</summary>
    Task<SignedVersionResult> UploadSignedVersionAsync(
        Guid documentId, Stream content, string fileName, string contentType, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Calls Documents.Api's POST /{documentId}/lock — marks the final signed artifact
    /// (never the original) permanently read-only right after it's uploaded.</summary>
    Task LockAsync(Guid documentId, CancellationToken cancellationToken = default);
}
