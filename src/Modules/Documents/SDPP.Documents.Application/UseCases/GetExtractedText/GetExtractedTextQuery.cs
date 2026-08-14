using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Application.UseCases.GetExtractedText;

public sealed record ExtractedTextResult(string FileName, string ExtractedText, IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// Backs the internal, service-to-service endpoint the Classification module calls to fetch
/// inspectable text (see docs/01-architecture/c4-diagrams.md, sync call from Document API to
/// Classification API — the content itself flows the other way, on demand, to avoid duplicating
/// storage of extracted text).
/// </summary>
public sealed record GetExtractedTextQuery(Guid DocumentId) : IQuery<ExtractedTextResult>;

public sealed class GetExtractedTextHandler(IDocumentRepository repository, IDocumentTextExtractionService textExtractionService)
    : IRequestHandler<GetExtractedTextQuery, Result<ExtractedTextResult>>
{
    public async Task<Result<ExtractedTextResult>> Handle(GetExtractedTextQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure<ExtractedTextResult>("El documento no existe.", "DOCUMENT_NOT_FOUND");
        }

        var extracted = await textExtractionService.ExtractAsync(document, cancellationToken);
        return Result.Success(new ExtractedTextResult(document.OriginalFileName, extracted.ExtractedText, extracted.Metadata));
    }
}
