using Microsoft.Extensions.Logging;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// Produces a searchable PDF (image + an invisible OCR text layer) via Tesseract's own `pdf`
/// output mode, which is what actually keeps the result openable and visually identical to the
/// source — this never rewrites pixels, only adds a text layer on top. A scanned PDF input is
/// rasterized page-by-page first (pdftoppm), each page OCR'd independently, then reassembled with
/// `pdfunite` — Tesseract itself only accepts a single image per invocation.
/// </summary>
public sealed class TesseractOcrEngine(ILogger<TesseractOcrEngine> logger) : IConversionEngine
{
    private const string Languages = "spa+eng";

    public bool CanHandle(OperationType operationType) => operationType == OperationType.Ocr;

    public async Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        var inputFilePath = inputFilePaths[0];

        return await IsPdfAsync(inputFilePath)
            ? await OcrPdfAsync(inputFilePath, cancellationToken)
            : await OcrImageAsync(inputFilePath, cancellationToken);
    }

    private async Task<ConversionEngineResult> OcrImageAsync(string inputFilePath, CancellationToken ct)
    {
        var outputBase = Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}");
        var result = await RunTesseractAsync(inputFilePath, outputBase, ct);
        return result.Success
            ? new ConversionEngineResult(true, outputBase + ".pdf", "Tesseract", null)
            : result;
    }

    private async Task<ConversionEngineResult> OcrPdfAsync(string inputFilePath, CancellationToken ct)
    {
        var pagesDirectory = Path.Combine(Path.GetTempPath(), $"sdpp-pages-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pagesDirectory);
        var pagePrefix = Path.Combine(pagesDirectory, "page");

        var (rasterExit, _, rasterErr) = await ProcessRunner.RunAsync(
            "pdftoppm", ["-png", "-r", "300", inputFilePath, pagePrefix], TimeSpan.FromMinutes(5), ct);
        if (rasterExit != 0)
        {
            logger.LogWarning("pdftoppm terminó con código {ExitCode}: {StdErr}", rasterExit, rasterErr);
            return new ConversionEngineResult(false, null, "Tesseract", rasterErr);
        }

        var pageImages = Directory.GetFiles(pagesDirectory, "*.png").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (pageImages.Count == 0)
        {
            return new ConversionEngineResult(false, null, "Tesseract", "No se pudo rasterizar ninguna página del PDF.");
        }

        var ocrPagePdfs = new List<string>();
        foreach (var pageImage in pageImages)
        {
            var outputBase = Path.ChangeExtension(pageImage, null);
            var result = await RunTesseractAsync(pageImage, outputBase, ct);
            if (!result.Success)
            {
                return result;
            }
            ocrPagePdfs.Add(outputBase + ".pdf");
        }

        var mergedPath = Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}.pdf");
        // qpdf, not pdfunite: pdfunite concatenating tesseract's per-page output PDFs left a
        // trailer /Size that didn't match the actual highest object number (qpdf --check flagged
        // "reported number of objects is not one plus the highest object number" — readers
        // tolerate it via fallback reconstruction, but QpdfEngine.MergeAsync's own --empty
        // --pages merge produces zero such warnings, so reuse that instead of a second tool).
        var qpdfArgs = new List<string> { "--empty", "--pages" };
        qpdfArgs.AddRange(ocrPagePdfs);
        qpdfArgs.Add("--");
        qpdfArgs.Add(mergedPath);

        var (mergeExit, _, mergeErr) = await ProcessRunner.RunAsync("qpdf", qpdfArgs, TimeSpan.FromMinutes(5), ct);
        if (mergeExit != 0)
        {
            logger.LogWarning("qpdf terminó con código {ExitCode}: {StdErr}", mergeExit, mergeErr);
            return new ConversionEngineResult(false, null, "Tesseract", mergeErr);
        }

        return File.Exists(mergedPath)
            ? new ConversionEngineResult(true, mergedPath, "Tesseract", null)
            : new ConversionEngineResult(false, null, "Tesseract", "No se generó ningún archivo de salida.");
    }

    private async Task<ConversionEngineResult> RunTesseractAsync(string inputImagePath, string outputBase, CancellationToken ct)
    {
        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync(
            "tesseract", [inputImagePath, outputBase, "-l", Languages, "pdf"], TimeSpan.FromMinutes(5), ct);

        if (exitCode != 0)
        {
            logger.LogWarning("Tesseract terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            return new ConversionEngineResult(false, null, "Tesseract", stdErr);
        }

        return new ConversionEngineResult(true, null, "Tesseract", null);
    }

    private static async Task<bool> IsPdfAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var header = new byte[5];
        var read = await stream.ReadAsync(header.AsMemory(0, 5));
        return read == 5 && System.Text.Encoding.ASCII.GetString(header) == "%PDF-";
    }
}
