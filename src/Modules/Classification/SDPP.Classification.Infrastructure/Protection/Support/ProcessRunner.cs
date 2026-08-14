using System.Diagnostics;

namespace SDPP.Classification.Infrastructure.Protection.Support;

/// <summary>
/// Shared "run an external CLI tool safely" helper for the protection stack (qpdf). Duplicated
/// (not shared) from SDPP.Documents.Infrastructure.Engines.ProcessRunner — that copy is
/// `internal` and still needed there by the qpdf/poppler/ghostscript/tesseract conversion
/// engines, out of scope for the "Clasificación de Activos de Información" extraction; this
/// module cannot reference Documents.Infrastructure at all.
/// </summary>
internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // Array-based args only — never a shell string — to eliminate command-injection risk
        // (docs/05-security/threat-model-stride.md, E3).
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new TimeoutException($"'{fileName}' excedió el tiempo límite de {timeout.TotalSeconds:F0}s.");
        }

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }
}
