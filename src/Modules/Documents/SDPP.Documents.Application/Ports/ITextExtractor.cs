namespace SDPP.Documents.Application.Ports;

/// <summary>
/// Extracts plain text from a document so the Classification module can run its detectors over
/// it (see docs/05-security/classification-engine.md §3.1) without needing to parse Office/PDF
/// binaries itself.
/// </summary>
public interface ITextExtractor
{
    bool CanHandle(string contentType);
    Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default);
}
