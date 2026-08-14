using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// Wraps `soffice --headless --convert-to`. Runs inside the sandboxed sdpp-workers namespace
/// with no network egress (docs/07-operations/kubernetes-architecture.md §2-4) — this class only
/// knows how to invoke the local binary, never a remote/cloud API, matching the "no cloud
/// dependency" requirement of the project.
/// </summary>
public sealed class LibreOfficeEngine(ILogger<LibreOfficeEngine> logger) : IConversionEngine
{
    // Deliberately Office → PDF only. LibreOffice's PDF *import* filter always produces a Draw
    // document internally (page-as-graphics), which the Writer/Calc/Impress export filters then
    // reject — headless `--convert-to docx` FROM a pdf silently produces nothing or a corrupt
    // file no matter how the target filter is spelled. The PdfToWord/PdfToExcel/PdfToPpt
    // directions are handled by PdfReconstructionEngine instead (Engines/PdfReconstructionEngine.cs),
    // which builds valid OOXML directly rather than asking LibreOffice to do something it
    // fundamentally cannot do reliably.
    private static readonly HashSet<OperationType> SupportedOperations =
    [
        OperationType.WordToPdf, OperationType.ExcelToPdf, OperationType.PptToPdf, OperationType.ImageToPdf,
    ];

    private static readonly Dictionary<OperationType, string> TargetFilters = new()
    {
        [OperationType.WordToPdf] = "pdf",
        [OperationType.ExcelToPdf] = "pdf",
        [OperationType.PptToPdf] = "pdf",
        [OperationType.ImageToPdf] = "pdf",
    };

    public bool CanHandle(OperationType operationType) => SupportedOperations.Contains(operationType);

    public async Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        var inputFilePath = inputFilePaths[0];
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        var targetFilter = TargetFilters[operationType];

        // Arguments passed as an array (never concatenated into a shell string) to eliminate
        // command-injection risk — see docs/05-security/threat-model-stride.md, E3.
        var startInfo = new ProcessStartInfo
        {
            FileName = "soffice",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--norestore");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(targetFilter);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add(inputFilePath);

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5)); // hard timeout — mitigates STRIDE D1 (zip bombs / hostile files)

        process.Start();
        // Both streams must be drained concurrently with WaitForExitAsync — soffice can write
        // enough to stdout that, left unread, fills the OS pipe buffer and blocks the child
        // process forever, which previously hung every job until the 5-minute timeout.
        var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new ConversionEngineResult(false, null, "LibreOffice", "Tiempo de conversión excedido (timeout).");
        }

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        if (process.ExitCode != 0)
        {
            logger.LogWarning("LibreOffice terminó con código {ExitCode}: {StdErr}", process.ExitCode, stdErr);
            return new ConversionEngineResult(false, null, "LibreOffice", stdErr);
        }

        logger.LogDebug("LibreOffice stdout: {StdOut}", stdOut);

        var outputFile = Directory.GetFiles(outputDirectory).FirstOrDefault();
        return outputFile is null
            ? new ConversionEngineResult(false, null, "LibreOffice", "No se generó ningún archivo de salida.")
            : new ConversionEngineResult(true, outputFile, "LibreOffice", null);
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
    }
}
