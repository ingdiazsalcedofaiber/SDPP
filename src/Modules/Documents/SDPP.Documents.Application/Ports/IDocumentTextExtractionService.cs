using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Application.Ports;

public sealed record ExtractedContent(string ExtractedText, IReadOnlyDictionary<string, string> Metadata);

/// <summary>Shared "pick the right ITextExtractor for this document's ContentType, open its blob,
/// extract text" logic — used by both GetExtractedTextHandler (serves Classification.Api) and the
/// fingerprint step in RequestConversionHandler, so the two never drift on how text gets pulled
/// out of a stored document.</summary>
public interface IDocumentTextExtractionService
{
    Task<ExtractedContent> ExtractAsync(DocumentInstance document, CancellationToken cancellationToken = default);
}
