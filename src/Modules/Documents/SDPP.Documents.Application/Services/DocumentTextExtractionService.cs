using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Application.Services;

public sealed class DocumentTextExtractionService(IBlobStorage blobStorage, IEnumerable<ITextExtractor> extractors)
    : IDocumentTextExtractionService
{
    public async Task<ExtractedContent> ExtractAsync(DocumentInstance document, CancellationToken cancellationToken = default)
    {
        var extractor = extractors.FirstOrDefault(e => e.CanHandle(document.ContentType));
        var text = string.Empty;

        if (extractor is not null)
        {
            await using var content = await blobStorage.OpenReadAsync(document.StorageLocation, cancellationToken);
            text = await extractor.ExtractTextAsync(content, cancellationToken);
        }

        var metadata = new Dictionary<string, string> { ["ContentType"] = document.ContentType };
        return new ExtractedContent(text, metadata);
    }
}
