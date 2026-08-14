using System.IO.Compression;
using Microsoft.Extensions.Logging;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// Wraps `pdftoppm` (Poppler) to rasterize every page of a PDF. A single-page source yields the
/// image file directly; a multi-page source yields a .zip of one image per page — Poppler's
/// renderer is a mature, purpose-built rasterizer, unlike asking LibreOffice to do it via its
/// Draw import path (see LibreOfficeEngine's SupportedOperations comment for why that's avoided).
/// </summary>
public sealed class PdfToImageEngine(ILogger<PdfToImageEngine> logger) : IConversionEngine
{
    public bool CanHandle(OperationType operationType) => operationType == OperationType.PdfToImage;

    public async Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        var inputFilePath = inputFilePaths[0];
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var outputPrefix = Path.Combine(outputDirectory, "page");

        var format = parameters.GetValueOrDefault("format", "png").ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => "-jpeg",
            _ => "-png",
        };

        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync(
            "pdftoppm",
            [format, "-r", "150", inputFilePath, outputPrefix],
            TimeSpan.FromMinutes(5),
            cancellationToken);

        if (exitCode != 0)
        {
            logger.LogWarning("pdftoppm terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            return new ConversionEngineResult(false, null, "Poppler", stdErr);
        }

        var pages = Directory.GetFiles(outputDirectory).OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (pages.Count == 0)
        {
            return new ConversionEngineResult(false, null, "Poppler", "No se generó ninguna imagen de página.");
        }

        if (pages.Count == 1)
        {
            return new ConversionEngineResult(true, pages[0], "Poppler", null);
        }

        var zipPath = Path.Combine(Path.GetTempPath(), $"sdpp-pages-{Guid.NewGuid():N}.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var page in pages)
            {
                zip.CreateEntryFromFile(page, Path.GetFileName(page));
            }
        }

        return new ConversionEngineResult(true, zipPath, "Poppler", null);
    }
}
