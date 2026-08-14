using System.Diagnostics;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>Wraps Poppler's `pdftotext` (see docs/01-architecture/technology-stack.md §1). Only
/// handles PDF; Office formats are converted to PDF first by the caller when extraction is needed
/// for a non-PDF upload (out of scope for this scaffold — PDF is the representative case).</summary>
public sealed class PopplerTextExtractor : ITextExtractor
{
    public bool CanHandle(string contentType) => contentType == "application/pdf";

    public async Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"sdpp-extract-{Guid.NewGuid():N}.pdf");
        var outputPath = Path.ChangeExtension(inputPath, ".txt");

        await using (var file = new FileStream(inputPath, FileMode.Create, FileAccess.Write))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pdftotext",
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(outputPath);

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0 && File.Exists(outputPath)
                ? await File.ReadAllTextAsync(outputPath, cancellationToken)
                : string.Empty;
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort cleanup */ }
    }
}
