using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// Before this class, <see cref="PopplerTextExtractor"/> was the *only* registered
/// <see cref="ITextExtractor"/> in the whole platform — meaning any Word document was classified
/// against empty text (GetExtractedTextHandler finds no matching extractor and returns
/// <c>string.Empty</c>). That's a real, independent bug this integrity/fingerprint work depends
/// on being fixed: a fingerprint over an empty string is useless for telling two Word documents
/// apart. XLSX/PPTX extractors are intentionally out of scope for this phase.
/// </summary>
public sealed class DocxTextExtractor : ITextExtractor
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public bool CanHandle(string contentType) => contentType == DocxContentType;

    public async Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
    {
        // WordprocessingDocument.Open needs random access (it reads the OPC/ZIP package structure)
        // — buffer to memory first since a blob-storage read stream isn't guaranteed seekable.
        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        using var document = WordprocessingDocument.Open(buffered, isEditable: false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var text in body.Descendants<Text>())
        {
            builder.Append(text.Text);
            builder.Append(' ');
        }
        return builder.ToString();
    }
}
