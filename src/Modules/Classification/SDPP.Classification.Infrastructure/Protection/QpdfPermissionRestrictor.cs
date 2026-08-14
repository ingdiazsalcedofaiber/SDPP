using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SDPP.Classification.Infrastructure.Protection.Support;

namespace SDPP.Classification.Infrastructure.Protection;

/// <summary>
/// Restringido-and-above uses qpdf encryption purely for its permission bits, not to gate opening
/// the file — distinct from Documents.Infrastructure's own QpdfEngine.Protect operation, which
/// sets a real user password the caller chooses. Reuses the shared ProcessRunner (this module's
/// own copy — see Protection/Support/ProcessRunner.cs) rather than a second process-invocation path.
/// </summary>
public interface IPdfPermissionRestrictor
{
    Task<string> RestrictPrintAndCopyAsync(string inputPdfPath, CancellationToken cancellationToken = default);
}

public sealed class QpdfPermissionRestrictor(ILogger<QpdfPermissionRestrictor> logger) : IPdfPermissionRestrictor
{
    public async Task<string> RestrictPrintAndCopyAsync(string inputPdfPath, CancellationToken cancellationToken = default)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"sdpp-restricted-{Guid.NewGuid():N}.pdf");

        // Empty user password: the document still opens with no prompt — what every PDF viewer
        // actually enforces is the permission bits below, gated by the (random, immediately
        // discarded) owner password qpdf requires to set them at all.
        var ownerPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync(
            "qpdf",
            [
                "--encrypt", "", ownerPassword, "256",
                "--print=none", "--modify=none", "--extract=n", "--annotate=n",
                "--", inputPdfPath, outputPath,
            ],
            TimeSpan.FromMinutes(5), cancellationToken);

        if (exitCode != 0 || !File.Exists(outputPath))
        {
            logger.LogWarning("qpdf --encrypt (restricción de permisos) terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            throw new InvalidOperationException($"No se pudieron aplicar las restricciones de impresión/copia: {stdErr}");
        }

        return outputPath;
    }
}
