using System.IO.Compression;
using Microsoft.Extensions.Logging;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// Wraps `qpdf` — a structural PDF tool (not a rendering/rasterizing one), so page
/// add/remove/reorder/rotate and encrypt/decrypt never touch content streams and can't introduce
/// rendering artifacts. This is the same category of tool production PDF pipelines use for these
/// exact operations (pdftk-style), rather than asking a document editor to do structural surgery.
/// </summary>
public sealed class QpdfEngine(ILogger<QpdfEngine> logger) : IConversionEngine
{
    private static readonly HashSet<OperationType> SupportedOperations =
    [
        OperationType.Merge, OperationType.Split, OperationType.Rotate,
        OperationType.DeletePages, OperationType.ReorderPages, OperationType.Protect, OperationType.Unlock,
    ];

    public bool CanHandle(OperationType operationType) => SupportedOperations.Contains(operationType);

    public Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default) => operationType switch
    {
        OperationType.Merge => MergeAsync(inputFilePaths, cancellationToken),
        OperationType.Split => SplitAsync(inputFilePaths[0], cancellationToken),
        OperationType.Rotate => RotateAsync(inputFilePaths[0], parameters, cancellationToken),
        OperationType.DeletePages => DeletePagesAsync(inputFilePaths[0], parameters, cancellationToken),
        OperationType.ReorderPages => ReorderPagesAsync(inputFilePaths[0], parameters, cancellationToken),
        OperationType.Protect => ProtectAsync(inputFilePaths[0], parameters, cancellationToken),
        OperationType.Unlock => UnlockAsync(inputFilePaths[0], parameters, cancellationToken),
        _ => throw new NotSupportedException($"QpdfEngine no maneja '{operationType}'."),
    };

    private async Task<ConversionEngineResult> MergeAsync(IReadOnlyList<string> inputFilePaths, CancellationToken ct)
    {
        if (inputFilePaths.Count < 2)
        {
            return new ConversionEngineResult(false, null, "qpdf",
                "Merge requiere al menos dos documentos — indique 'additionalDocumentIds' en los parámetros de la operación.");
        }

        var outputPath = NewTempFile(".pdf");
        // qpdf --empty --pages A B C -- out.pdf : concatenate every page of every input, in order.
        var args = new List<string> { "--empty", "--pages" };
        args.AddRange(inputFilePaths);
        args.Add("--");
        args.Add(outputPath);

        return await RunQpdfAsync(args, outputPath, "qpdf", ct);
    }

    private async Task<ConversionEngineResult> SplitAsync(string inputFilePath, CancellationToken ct)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var outputPattern = Path.Combine(outputDirectory, "page-%d.pdf");

        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync(
            "qpdf", ["--split-pages", inputFilePath, outputPattern], TimeSpan.FromMinutes(5), ct);

        if (exitCode != 0)
        {
            logger.LogWarning("qpdf --split-pages terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            return new ConversionEngineResult(false, null, "qpdf", stdErr);
        }

        var parts = Directory.GetFiles(outputDirectory).OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (parts.Count == 0)
        {
            return new ConversionEngineResult(false, null, "qpdf", "No se generó ninguna página.");
        }

        var zipPath = NewTempFile(".zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var part in parts)
            {
                zip.CreateEntryFromFile(part, Path.GetFileName(part));
            }
        }

        return new ConversionEngineResult(true, zipPath, "qpdf", null);
    }

    private async Task<ConversionEngineResult> RotateAsync(
        string inputFilePath, IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        var angle = parameters.GetValueOrDefault("angle", "90");
        if (angle is not ("90" or "180" or "270" or "-90" or "-180" or "-270"))
        {
            return new ConversionEngineResult(false, null, "qpdf", "El ángulo de rotación debe ser 90, 180 o 270 (o su negativo).");
        }

        var pageRange = parameters.GetValueOrDefault("pages", "1-z");
        var outputPath = NewTempFile(".pdf");

        // Combined --option=value form — qpdf's own docs use this form for --rotate, and it
        // avoids any ambiguity about whether a following argv entry is the value or a new flag.
        return await RunQpdfAsync(
            [$"--rotate={angle}:{pageRange}", inputFilePath, "--", outputPath], outputPath, "qpdf", ct);
    }

    private async Task<ConversionEngineResult> DeletePagesAsync(
        string inputFilePath, IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        if (!parameters.TryGetValue("pages", out var pagesToDeleteSpec) || string.IsNullOrWhiteSpace(pagesToDeleteSpec))
        {
            return new ConversionEngineResult(false, null, "qpdf", "Debe indicar 'pages' (las páginas a eliminar, ej. '2,4-6').");
        }

        var pageCountResult = await GetPageCountAsync(inputFilePath, ct);
        if (pageCountResult is not { } totalPages)
        {
            return new ConversionEngineResult(false, null, "qpdf", "No se pudo determinar el número de páginas del documento.");
        }

        HashSet<int> toDelete;
        try
        {
            toDelete = ParsePageSet(pagesToDeleteSpec, totalPages);
        }
        catch (FormatException ex)
        {
            return new ConversionEngineResult(false, null, "qpdf", ex.Message);
        }

        // qpdf has no "remove these pages" verb — express it as "keep everything else" instead,
        // computed here rather than trusting a CLI flag whose exact syntax varies by qpdf version.
        var toKeep = Enumerable.Range(1, totalPages).Where(p => !toDelete.Contains(p)).ToList();
        if (toKeep.Count == 0)
        {
            return new ConversionEngineResult(false, null, "qpdf", "No puede eliminar todas las páginas del documento.");
        }

        var outputPath = NewTempFile(".pdf");
        var keepSelection = string.Join(",", toKeep);

        return await RunQpdfAsync(
            [inputFilePath, "--pages", ".", keepSelection, "--", outputPath], outputPath, "qpdf", ct);
    }

    private async Task<ConversionEngineResult> ReorderPagesAsync(
        string inputFilePath, IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        if (!parameters.TryGetValue("order", out var order) || string.IsNullOrWhiteSpace(order))
        {
            return new ConversionEngineResult(false, null, "qpdf", "Debe indicar 'order' (el nuevo orden de páginas, ej. '3,1,2').");
        }

        var pageSelection = string.Join(",", order.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        if (pageSelection.Length == 0)
        {
            return new ConversionEngineResult(false, null, "qpdf", "'order' no contiene ningún número de página válido.");
        }

        var outputPath = NewTempFile(".pdf");
        return await RunQpdfAsync(
            [inputFilePath, "--pages", ".", pageSelection, "--", outputPath], outputPath, "qpdf", ct);
    }

    private async Task<ConversionEngineResult> ProtectAsync(
        string inputFilePath, IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        if (!parameters.TryGetValue("password", out var password) || string.IsNullOrWhiteSpace(password))
        {
            return new ConversionEngineResult(false, null, "qpdf", "Debe indicar 'password' para proteger el documento.");
        }

        var ownerPassword = parameters.GetValueOrDefault("ownerPassword", password);
        var outputPath = NewTempFile(".pdf");

        return await RunQpdfAsync(
            ["--encrypt", password, ownerPassword, "256", "--", inputFilePath, outputPath], outputPath, "qpdf", ct);
    }

    private async Task<ConversionEngineResult> UnlockAsync(
        string inputFilePath, IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        var password = parameters.GetValueOrDefault("password", string.Empty);
        var outputPath = NewTempFile(".pdf");

        var args = new List<string>();
        if (!string.IsNullOrEmpty(password))
        {
            args.Add($"--password={password}");
        }
        args.Add("--decrypt");
        args.Add(inputFilePath);
        args.Add(outputPath);

        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync("qpdf", args, TimeSpan.FromMinutes(5), ct);
        if (exitCode != 0)
        {
            logger.LogWarning("qpdf --decrypt terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            return new ConversionEngineResult(false, null, "qpdf",
                stdErr.Contains("invalid password", StringComparison.OrdinalIgnoreCase)
                    ? "La contraseña indicada no es correcta."
                    : stdErr);
        }

        return File.Exists(outputPath)
            ? new ConversionEngineResult(true, outputPath, "qpdf", null)
            : new ConversionEngineResult(false, null, "qpdf", "No se generó ningún archivo de salida.");
    }

    private async Task<int?> GetPageCountAsync(string inputFilePath, CancellationToken ct)
    {
        var (exitCode, stdOut, _) = await ProcessRunner.RunAsync(
            "qpdf", ["--show-npages", inputFilePath], TimeSpan.FromMinutes(1), ct);
        return exitCode == 0 && int.TryParse(stdOut.Trim(), out var pages) ? pages : null;
    }

    /// <summary>Parses "2,4-6" style page specs into a validated set of 1-based page numbers.</summary>
    private static HashSet<int> ParsePageSet(string spec, int totalPages)
    {
        var result = new HashSet<int>();
        foreach (var token in spec.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Contains('-'))
            {
                var parts = token.Split('-', 2);
                if (!int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end) || start > end)
                {
                    throw new FormatException($"Rango de páginas inválido: '{token}'.");
                }
                for (var p = start; p <= end; p++)
                {
                    result.Add(p);
                }
            }
            else if (int.TryParse(token, out var page))
            {
                result.Add(page);
            }
            else
            {
                throw new FormatException($"Número de página inválido: '{token}'.");
            }
        }

        if (result.Any(p => p < 1 || p > totalPages))
        {
            throw new FormatException($"Las páginas indicadas deben estar entre 1 y {totalPages}.");
        }

        return result;
    }

    private async Task<ConversionEngineResult> RunQpdfAsync(
        IReadOnlyList<string> args, string outputPath, string engineLabel, CancellationToken ct)
    {
        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync("qpdf", args, TimeSpan.FromMinutes(5), ct);
        if (exitCode != 0)
        {
            logger.LogWarning("qpdf terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            return new ConversionEngineResult(false, null, engineLabel, stdErr);
        }

        return File.Exists(outputPath)
            ? new ConversionEngineResult(true, outputPath, engineLabel, null)
            : new ConversionEngineResult(false, null, engineLabel, "No se generó ningún archivo de salida.");
    }

    private static string NewTempFile(string extension) =>
        Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}{extension}");
}
