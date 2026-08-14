using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Application.Ports;

public sealed record ConversionEngineResult(bool Success, string? OutputFilePath, string EngineUsed, string? ErrorDetail);

/// <summary>
/// Port to a document conversion engine (LibreOffice, Ghostscript, PDFBox, Tesseract — see
/// docs/01-architecture/technology-stack.md §4). Each implementation wraps a single external
/// binary and never receives untrusted input via a shell string — arguments are always passed as
/// an array to avoid command injection (docs/05-security/threat-model-stride.md, E3).
/// </summary>
public interface IConversionEngine
{
    bool CanHandle(OperationType operationType);

    /// <summary>
    /// Almost every operation has exactly one input file; Merge is the exception (see
    /// ConversionRequestedConsumer, which resolves "additionalDocumentIds" from the request
    /// parameters into the extra entries here before invoking the engine).
    /// </summary>
    Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);
}
