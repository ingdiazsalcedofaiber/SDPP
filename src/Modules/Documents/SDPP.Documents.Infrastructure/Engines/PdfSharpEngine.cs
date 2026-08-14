using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// In-process PDF page overlays (watermark text, page numbers) via PdfSharp — no external
/// process, and PdfSharp opens/edits the existing PDF object graph directly rather than
/// re-rendering it, so nothing but the requested overlay changes.
/// </summary>
public sealed class PdfSharpEngine : IConversionEngine
{
    private static readonly HashSet<OperationType> SupportedOperations =
    [
        OperationType.Watermark, OperationType.PageNumbering,
    ];

    static PdfSharpEngine()
    {
        // Must be set before the first XFont is created anywhere in the process; a static
        // constructor guarantees that regardless of DI registration order.
        GlobalFontSettings.FontResolver ??= new PdfSharpFontResolver();
    }

    public PdfSharpEngine(ILogger<PdfSharpEngine> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<PdfSharpEngine> _logger;

    public bool CanHandle(OperationType operationType) => SupportedOperations.Contains(operationType);

    public Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        var inputFilePath = inputFilePaths[0];
        return Task.FromResult(operationType switch
        {
            OperationType.Watermark => ApplyWatermark(inputFilePath, parameters),
            OperationType.PageNumbering => ApplyPageNumbers(inputFilePath, parameters),
            _ => throw new NotSupportedException($"PdfSharpEngine no maneja '{operationType}'."),
        });
    }

    private ConversionEngineResult ApplyWatermark(string inputFilePath, IReadOnlyDictionary<string, string> parameters)
    {
        var text = parameters.GetValueOrDefault("text", "CONFIDENCIAL");

        try
        {
            using var document = PdfReader.Open(inputFilePath, PdfDocumentOpenMode.Modify);
            var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, 54);
            var brush = new XSolidBrush(XColor.FromArgb(90, 200, 0, 0));

            foreach (var page in document.Pages.Cast<PdfPage>())
            {
                // .Point extracted up front — PDFsharp 6.1 deliberately made XUnit's implicit
                // conversion to/from double obsolete, so every arithmetic op below must work in
                // plain doubles rather than relying on that conversion happening silently.
                var pageWidth = page.Width.Point;
                var pageHeight = page.Height.Point;

                using var gfx = XGraphics.FromPdfPage(page);
                var size = gfx.MeasureString(text, font);

                gfx.TranslateTransform(pageWidth / 2, pageHeight / 2);
                gfx.RotateTransform(-45);
                gfx.DrawString(text, font, brush, -size.Width / 2, size.Height / 2);
            }

            var outputPath = NewTempFile(".pdf");
            document.Save(outputPath);
            return new ConversionEngineResult(true, outputPath, "PdfSharp", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PdfSharp falló al aplicar la marca de agua");
            return new ConversionEngineResult(false, null, "PdfSharp", ex.Message);
        }
    }

    private ConversionEngineResult ApplyPageNumbers(string inputFilePath, IReadOnlyDictionary<string, string> parameters)
    {
        // {page} / {total} are the only placeholders — kept simple and explicit rather than a
        // general templating engine for two substitutions.
        var format = parameters.GetValueOrDefault("format", "Página {page} de {total}");

        try
        {
            using var document = PdfReader.Open(inputFilePath, PdfDocumentOpenMode.Modify);
            var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, 10);
            var brush = new XSolidBrush(XColors.Black);
            var totalPages = document.PageCount;

            var pageNumber = 1;
            foreach (var page in document.Pages.Cast<PdfPage>())
            {
                var text = format.Replace("{page}", pageNumber.ToString()).Replace("{total}", totalPages.ToString());
                var pageWidth = page.Width.Point;
                var pageHeight = page.Height.Point;

                using var gfx = XGraphics.FromPdfPage(page);
                var size = gfx.MeasureString(text, font);
                gfx.DrawString(text, font, brush, (pageWidth - size.Width) / 2, pageHeight - size.Height - 10);
                pageNumber++;
            }

            var outputPath = NewTempFile(".pdf");
            document.Save(outputPath);
            return new ConversionEngineResult(true, outputPath, "PdfSharp", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PdfSharp falló al numerar las páginas");
            return new ConversionEngineResult(false, null, "PdfSharp", ex.Message);
        }
    }

    private static string NewTempFile(string extension) =>
        Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}{extension}");
}
