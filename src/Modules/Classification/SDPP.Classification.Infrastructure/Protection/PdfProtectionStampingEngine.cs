using System.Globalization;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SDPP.Classification.Application.Ports;
using SDPP.Classification.Infrastructure.Protection.Support;

namespace SDPP.Classification.Infrastructure.Protection;

/// <summary>
/// Stamps a conversion output PDF according to a resolved ProtectionLevelPolicy — diagonal
/// repeated watermark, footer, header, and PDF metadata. Reuses PdfSharp + PdfSharpFontResolver
/// rather than a second font-loading path; the watermark here is unconditionally tiled across the
/// page (spec: "nunca una marca fija", must read as clearly automatic/pervasive protection rather
/// than a single corner stamp).
/// </summary>
public interface IProtectionStampingEngine
{
    string Stamp(string inputPdfPath, ProtectionLevelPolicy policy, ProtectionContext context, string renderedWatermark, string renderedFooter, string renderedHeader);
}

public sealed class PdfProtectionStampingEngine(ILogger<PdfProtectionStampingEngine> logger) : IProtectionStampingEngine
{
    static PdfProtectionStampingEngine()
    {
        GlobalFontSettings.FontResolver ??= new PdfSharpFontResolver();
    }

    public string Stamp(
        string inputPdfPath, ProtectionLevelPolicy policy, ProtectionContext context,
        string renderedWatermark, string renderedFooter, string renderedHeader)
    {
        using var document = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Modify);

        if (policy.EmbedMetadata)
        {
            StampMetadata(document, context);
        }

        foreach (var page in document.Pages.Cast<PdfPage>())
        {
            var pageWidth = page.Width.Point;
            var pageHeight = page.Height.Point;
            using var gfx = XGraphics.FromPdfPage(page);

            if (policy.ApplyWatermark)
            {
                DrawTiledWatermark(gfx, pageWidth, pageHeight, renderedWatermark, policy.Watermark);
            }

            if (policy.ApplyFooter)
            {
                DrawFooter(gfx, pageWidth, pageHeight, renderedFooter);
            }

            if (policy.ApplyHeader)
            {
                DrawHeader(gfx, pageWidth, renderedHeader);
            }
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"sdpp-protected-{Guid.NewGuid():N}.pdf");
        document.Save(outputPath);
        logger.LogInformation(
            "Protección aplicada al documento {DocumentId} (auditId {AuditId}): watermark={Watermark} footer={Footer} header={Header} metadata={Metadata}",
            context.DocumentId, context.AuditId, policy.ApplyWatermark, policy.ApplyFooter, policy.ApplyHeader, policy.EmbedMetadata);
        return outputPath;
    }

    private static void DrawTiledWatermark(XGraphics gfx, double pageWidth, double pageHeight, string text, WatermarkStyle style)
    {
        var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, style.FontSize);
        var (r, g, b) = ParseColor(style.ColorHex);
        var color = XColor.FromArgb((int)Math.Round(style.Opacity * 255), r, g, b);
        var brush = new XSolidBrush(color);
        var size = gfx.MeasureString(text, font);

        // Tiled grid, not a single stamp — repeats the text across the whole page so it can't be
        // cropped out, matching the spec's "repetida en todas las páginas" requirement.
        var pitchX = size.Width + 60;
        var pitchY = size.Height + 60;
        var diagonal = Math.Sqrt(pageWidth * pageWidth + pageHeight * pageHeight);
        var steps = (int)Math.Ceiling(diagonal / Math.Min(pitchX, pitchY)) + 2;

        gfx.Save();
        gfx.TranslateTransform(pageWidth / 2, pageHeight / 2);
        gfx.RotateTransform(style.AngleDegrees);

        for (var row = -steps; row <= steps; row++)
        {
            for (var col = -steps; col <= steps; col++)
            {
                gfx.DrawString(text, font, brush, col * pitchX - size.Width / 2, row * pitchY + size.Height / 2);
            }
        }

        gfx.Restore();
    }

    private static void DrawFooter(XGraphics gfx, double pageWidth, double pageHeight, string text)
    {
        var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, 8);
        var brush = new XSolidBrush(XColors.Black);
        var size = gfx.MeasureString(text, font);
        gfx.DrawString(text, font, brush, (pageWidth - size.Width) / 2, pageHeight - 12);
    }

    private static void DrawHeader(XGraphics gfx, double pageWidth, string text)
    {
        var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, 8);
        var brush = new XSolidBrush(XColors.Black);
        var size = gfx.MeasureString(text, font);
        gfx.DrawString(text, font, brush, (pageWidth - size.Width) / 2, 16);
    }

    private static void StampMetadata(PdfDocument document, ProtectionContext context)
    {
        document.Info.Subject = $"Clasificación: {context.Classification}";
        document.Info.Keywords = string.Join(
            ";",
            new[] { context.Classification.ToString(), context.Category }
                .Concat(context.Labels)
                .Where(v => !string.IsNullOrWhiteSpace(v)));

        // PdfSharp's PdfDocumentInformation only exposes the standard Info dictionary keys —
        // riskScore/auditId/createdBy go into custom /Info entries via the underlying Elements
        // dictionary (still standard PDF, just not one of PdfSharp's typed wrapper properties).
        document.Info.Elements["/SDPP_AuditId"] = new PdfSharp.Pdf.PdfString(context.AuditId.ToString());
        document.Info.Elements["/SDPP_RiskScore"] = new PdfSharp.Pdf.PdfString(context.RiskScore.ToString(CultureInfo.InvariantCulture));
        document.Info.Elements["/SDPP_ProtectedBy"] = new PdfSharp.Pdf.PdfString(context.Actor.FullName);
        document.Info.Elements["/SDPP_ProtectedAtUtc"] = new PdfSharp.Pdf.PdfString(DateTime.UtcNow.ToString("O"));
    }

    private static (byte R, byte G, byte B) ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        var r = Convert.ToByte(value[..2], 16);
        var g = Convert.ToByte(value[2..4], 16);
        var b = Convert.ToByte(value[4..6], 16);
        return (r, g, b);
    }
}
