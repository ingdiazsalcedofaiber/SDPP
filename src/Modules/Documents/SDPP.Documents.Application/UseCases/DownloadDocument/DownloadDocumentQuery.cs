using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Application.UseCases.DownloadDocument;

public sealed record DownloadDocumentResult(string FileName, string ContentType, Stream Content, Guid DocumentVersionId);

public sealed record DownloadDocumentQuery(Guid DocumentId) : IQuery<DownloadDocumentResult>;

/// <summary>
/// Streams the stored bytes for any document — the original upload or a conversion's output,
/// both of which are just rows in the same Documents table (see docs/03-data/er-model.md).
/// </summary>
public sealed class DownloadDocumentHandler(IDocumentRepository repository, IBlobStorage blobStorage, ICurrentActor currentActor)
    : IRequestHandler<DownloadDocumentQuery, Result<DownloadDocumentResult>>
{
    public async Task<Result<DownloadDocumentResult>> Handle(DownloadDocumentQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure<DownloadDocumentResult>("El documento no existe.", "DOCUMENT_NOT_FOUND");
        }
        if (!DocumentAuthorization.CanView(document, currentActor))
        {
            return Result.Failure<DownloadDocumentResult>("No tienes permiso para descargar este documento.", "FORBIDDEN");
        }

        var content = await blobStorage.OpenReadAsync(document.StorageLocation, cancellationToken);
        return Result.Success(new DownloadDocumentResult(document.OriginalFileName, document.ContentType, content, document.DocumentVersionId));
    }
}
