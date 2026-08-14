using Microsoft.Extensions.Logging;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// Wraps Ghostscript's pdfwrite device to recompress embedded images/fonts. `/ebook` is the
/// default preset (150dpi images) — a reasonable balance of size vs. fidelity for the
/// "compress this PDF" use case; callers can override via the "preset" parameter
/// (screen|ebook|printer|prepress, Ghostscript's own preset names).
/// </summary>
public sealed class GhostscriptCompressEngine(ILogger<GhostscriptCompressEngine> logger) : IConversionEngine
{
    private static readonly HashSet<string> AllowedPresets = ["screen", "ebook", "printer", "prepress"];

    public bool CanHandle(OperationType operationType) => operationType == OperationType.Compress;

    public async Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        var inputFilePath = inputFilePaths[0];
        var preset = parameters.GetValueOrDefault("preset", "ebook").ToLowerInvariant();
        if (!AllowedPresets.Contains(preset))
        {
            preset = "ebook";
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}.pdf");

        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync(
            "gs",
            [
                "-sDEVICE=pdfwrite", "-dCompatibilityLevel=1.4", $"-dPDFSETTINGS=/{preset}",
                "-dNOPAUSE", "-dBATCH", "-dQUIET",
                $"-sOutputFile={outputPath}", inputFilePath,
            ],
            TimeSpan.FromMinutes(5),
            cancellationToken);

        if (exitCode != 0)
        {
            logger.LogWarning("Ghostscript terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            return new ConversionEngineResult(false, null, "Ghostscript", stdErr);
        }

        if (!File.Exists(outputPath))
        {
            return new ConversionEngineResult(false, null, "Ghostscript", "No se generó ningún archivo de salida.");
        }

        var originalSize = new FileInfo(inputFilePath).Length;
        var compressedSize = new FileInfo(outputPath).Length;
        logger.LogInformation(
            "Compresión Ghostscript ({Preset}): {Original} → {Compressed} bytes", preset, originalSize, compressedSize);

        return new ConversionEngineResult(true, outputPath, "Ghostscript", null);
    }
}
