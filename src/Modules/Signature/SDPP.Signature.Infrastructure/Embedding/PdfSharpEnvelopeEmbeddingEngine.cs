using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using QRCoder;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;
using SDPP.Signature.Infrastructure.Embedding.Support;

namespace SDPP.Signature.Infrastructure.Embedding;

/// <summary>
/// Draws every recipient's resolved fields onto the source PDF, and — when a certificate is passed
/// — appends the audit certificate page, all within one PdfSharp document session. This used to be
/// two separate engines (embed fields, then a second open/save cycle to append the certificate
/// page); merged after discovering that opening a SECOND PdfDocument in the same process, once the
/// first one's fonts had already been used and disposed, reliably NullReferenceExceptions deep
/// inside PdfSharp's own font-typeface cache (OpenTypeFontFace.GetOrCreateFrom) — a real PdfSharp
/// 6.x limitation, not a resolver bug (see PdfSharpFontResolver, unchanged and correct on its own).
/// One document, one Save() call, sidesteps it entirely. Image-bearing fields (Signature/Initials/
/// Stamp) reuse the exact aspect-ratio-safe technique already proven in the original single-signer
/// engine (derive draw height from the PNG's own pixel dimensions, never trust the caller's height
/// fraction).
/// </summary>
public sealed class PdfSharpEnvelopeEmbeddingEngine(ILogger<PdfSharpEnvelopeEmbeddingEngine> logger) : IPdfEnvelopeEmbeddingEngine
{
    static PdfSharpEnvelopeEmbeddingEngine()
    {
        GlobalFontSettings.FontResolver ??= new PdfSharpFontResolver();
    }

    public EmbedResult Embed(string inputPdfPath, IReadOnlyList<ResolvedField> fields, AuditCertificateInfo? certificate = null)
    {
        using var document = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Modify);

        foreach (var field in fields)
        {
            // The legal-approval stamp is a whole-document authorization mark, not a single placed
            // field: once Gerencia Legal presses "Autorizar" it appears on EVERY page of the signed
            // document (at the same relative position/size the creator placed it at), never just
            // the one page it was originally dropped on. The certificate is a separate page built by
            // DrawCertificatePage below, outside this loop entirely, so it never receives the stamp.
            var isLegalStamp = field.Type == FieldType.LegalApprovalStamp;
            var firstPage = isLegalStamp ? 1 : field.PageNumber;
            var lastPage = isLegalStamp ? document.PageCount : field.PageNumber;

            for (var pageNumber = firstPage; pageNumber <= lastPage; pageNumber++)
            {
                if (pageNumber < 1 || pageNumber > document.PageCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(fields), pageNumber, $"El documento tiene {document.PageCount} página(s); no existe la página {pageNumber}.");
                }

                var page = document.Pages[pageNumber - 1];
                // "Reported" — PdfSharp swaps these for a 90/270-rotated page to reflect the page's
                // effective/visual size, which conveniently is exactly the space the frontend's
                // fractions are already relative to (pdf.js auto-applies /Rotate when rendering, so
                // the overlay's percentages are relative to the DISPLAYED page — see FieldOverlay.tsx).
                var pageWidth = page.Width.Point;
                var pageHeight = page.Height.Point;
                var rotate = NormalizeRotation(page.Elements.GetInteger("/Rotate"));
                using var gfx = XGraphics.FromPdfPage(page);

                var width = field.Width * pageWidth;
                var height = field.Height * pageHeight;
                var (centerX, centerY, contentRotation) = ResolveDrawCenter(
                    rotate, pageWidth, pageHeight, field.PositionX + field.Width / 2, field.PositionY + field.Height / 2);
                var x = centerX - width / 2;
                var y = centerY - height / 2;

                var state = contentRotation != 0 ? gfx.Save() : null;
                if (contentRotation != 0)
                {
                    gfx.TranslateTransform(centerX, centerY);
                    gfx.RotateTransform(contentRotation);
                    gfx.TranslateTransform(-centerX, -centerY);
                }

                if (isLegalStamp)
                {
                    // No hash/timestamp caption here — it's an authorization mark, not a personal
                    // signature; the evidence trail already lives in the certificate and audit log.
                    DrawLegalApprovalStamp(gfx, x, y, width, height);
                }
                else if (field.Type is FieldType.Signature or FieldType.Initials or FieldType.Stamp)
                {
                    DrawSignedByLabel(gfx, field.RecipientFullName, x, y, width);
                    DrawImage(gfx, field.SignatureImage, x, y, width, height);
                    DrawSignatureCaption(gfx, field.SignatureHash, field.FilledAtUtc, x, y, width, height);
                }
                else if (field.Type == FieldType.Checkbox)
                {
                    DrawCheckbox(gfx, field.Value, x, y, width, height);
                }
                else
                {
                    DrawText(gfx, field.Value, x, y, height);
                }

                if (state is not null)
                {
                    gfx.Restore(state);
                }
            }
        }

        var fieldCount = fields.Count;
        var pageCountBeforeCertificate = document.PageCount;

        if (certificate is not null)
        {
            DrawCertificatePage(document, certificate);
        }

        // Captured before Save() — PdfSharp throws if PageCount is accessed on a document that was
        // already saved (a real 6.1.1 quirk, hit and fixed earlier in this module's history).
        var totalPageCount = document.PageCount;

        var outputPath = Path.Combine(Path.GetTempPath(), $"sdpp-envelope-embedded-{Guid.NewGuid():N}.pdf");
        document.Save(outputPath);
        logger.LogInformation(
            "Sobre incrustado: {FieldCount} campos en {PageCount} páginas (certificado={HasCertificate}).",
            fieldCount, pageCountBeforeCertificate, certificate is not null);

        string? certificatePath = certificate is not null
            ? ExtractCertificatePages(outputPath, pageCountBeforeCertificate, totalPageCount)
            : null;

        return new EmbedResult(outputPath, certificatePath);
    }

    /// <summary>
    /// Splits the certificate page(s) off the already-saved combined file into their own standalone
    /// PDF, using ONLY <see cref="PdfDocument.AddPage(PdfPage)"/> structural page-copying — no
    /// <see cref="XGraphics"/>, no <see cref="XFont"/> on this new document at all. That restriction
    /// is deliberate and load-bearing: opening a second PdfDocument that draws TEXT/fonts in the
    /// same process as the first (font-using) one reliably crashes PdfSharp 6.1.1 (see this class's
    /// main doc comment). Pure page-import never touches that code path — confirmed empirically in
    /// a standalone probe before wiring this in (same discipline as the rotation fix above).
    /// </summary>
    private static string ExtractCertificatePages(string combinedPdfPath, int firstCertificatePageIndex, int totalPageCount)
    {
        using var source = PdfReader.Open(combinedPdfPath, PdfDocumentOpenMode.Import);
        using var certificateDocument = new PdfDocument();
        for (var i = firstCertificatePageIndex; i < totalPageCount; i++)
        {
            certificateDocument.AddPage(source.Pages[i]);
        }

        var certificatePath = Path.Combine(Path.GetTempPath(), $"sdpp-envelope-certificate-{Guid.NewGuid():N}.pdf");
        certificateDocument.Save(certificatePath);
        return certificatePath;
    }

    private static int NormalizeRotation(int rawRotate) => ((rawRotate % 360) + 360) % 360;

    /// <summary>
    /// Maps a field's center, given as a fraction of the DISPLAYED page (what the frontend editor
    /// and pdf.js both already work in — see FieldOverlay.tsx), to the point PdfSharp must actually
    /// be told to draw at, plus how many degrees to rotate the drawn content so it appears upright
    /// once the PDF viewer applies /Rotate for display. Derived and empirically verified (see
    /// rotation-probe scratch experiment) against PdfSharp 6.1.1's actual, non-obvious behavior for
    /// rotated pages: <see cref="PdfSharp.Pdf.PdfPage.Width"/>/<c>Height</c> report the page's
    /// swapped/effective size for 90°/270° pages (so page.Width.Point/page.Height.Point above are
    /// already what the frontend fractions are relative to — no extra swap needed there), BUT
    /// XGraphics' own top-left-to-native y-flip still uses that SAME reported (swapped) height as
    /// its flip basis even though the underlying MediaBox the content stream coordinates land in is
    /// unswapped — passing a naively-computed position straight through sends content off-page for
    /// 90°/270° pages. The "reportedHeight - nativeHeight + y" term below corrects exactly that.
    /// 180° pages don't swap Width/Height at all, so no such correction applies there.
    /// </summary>
    private static (double X, double Y, double ContentRotationDegrees) ResolveDrawCenter(
        int rotate, double reportedWidth, double reportedHeight, double centerFractionX, double centerFractionY)
    {
        switch (rotate)
        {
            case 90:
            {
                var nativeWidth = reportedHeight;
                var nativeHeight = reportedWidth;
                var x = centerFractionY * nativeWidth;
                var yNative = (1 - centerFractionX) * nativeHeight;
                var y = reportedHeight - nativeHeight + yNative;
                return (x, y, -90);
            }
            case 180:
            {
                var x = reportedWidth * (1 - centerFractionX);
                var y = reportedHeight * (1 - centerFractionY);
                return (x, y, 180);
            }
            case 270:
            {
                var nativeWidth = reportedHeight;
                var nativeHeight = reportedWidth;
                var x = nativeWidth * (1 - centerFractionY);
                var yNative = centerFractionX * nativeHeight;
                var y = reportedHeight - nativeHeight + yNative;
                return (x, y, 90);
            }
            default:
                return (centerFractionX * reportedWidth, centerFractionY * reportedHeight, 0);
        }
    }

    private static void DrawImage(XGraphics gfx, byte[]? imagePng, double x, double y, double width, double height)
    {
        if (imagePng is null || imagePng.Length == 0)
        {
            return;
        }

        // publiclyVisible:true is required — PdfSharp's image importer calls GetBuffer() internally.
        using var imageStream = new MemoryStream(imagePng, 0, imagePng.Length, writable: false, publiclyVisible: true);
        using var xImage = XImage.FromStream(imageStream);

        // Contain, never cover: scale by whichever axis is tighter so the drawn image can never
        // exceed the field's own box on either dimension, then center it — a field's configured
        // width/height is a hard boundary the signature must respect (never trust the aspect ratio
        // of the field itself to already match the image's).
        var scale = Math.Min(width / xImage.PixelWidth, height / xImage.PixelHeight);
        var drawWidth = xImage.PixelWidth * scale;
        var drawHeight = xImage.PixelHeight * scale;
        var drawX = x + (width - drawWidth) / 2;
        var drawY = y + (height - drawHeight) / 2;
        gfx.DrawImage(xImage, drawX, drawY, drawWidth, drawHeight);
    }

    /// <summary>The official "APROBADO GERENCIA LEGAL" stamp — the real artwork supplied by
    /// Gerencia Legal (see StampBytes), embedded as-is, never redrawn and never taken from
    /// field.SignatureImage. See FieldType.LegalApprovalStamp's doc comment: accepting client-
    /// supplied bytes for an official approval mark would make it forgeable — this method is the
    /// ONLY place this stamp's appearance is ever produced.</summary>
    private static void DrawLegalApprovalStamp(XGraphics gfx, double x, double y, double width, double height)
    {
        DrawImage(gfx, StampBytes, x, y, width, height);
    }

    /// <summary>Printed just above the signature/initials/stamp image — names whose mark this is,
    /// directly on the page, the same way a paper form's "Firmado por: ___" caption would.</summary>
    private static void DrawSignedByLabel(XGraphics gfx, string recipientFullName, double x, double y, double width)
    {
        var label = $"Firmado por: {recipientFullName}";
        var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, 7);
        gfx.DrawString(label, font, new XSolidBrush(XColor.FromArgb(90, 90, 90)), x, y - 4);
    }

    /// <summary>Printed just below the signature/initials/stamp image itself — a short, visible
    /// tie between the mark on the page and its full evidence trail (the complete hash + timeline
    /// lives on the certificate; this is a compact on-page reference, not a duplicate of it).</summary>
    private static void DrawSignatureCaption(XGraphics gfx, string? signatureHash, DateTime? filledAtUtc, double x, double y, double width, double height)
    {
        if (string.IsNullOrEmpty(signatureHash) || filledAtUtc is null)
        {
            return;
        }

        var shortHash = signatureHash.Length > 12 ? signatureHash[..12] : signatureHash;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(filledAtUtc.Value, DateTimeKind.Utc), CertificateTimeZone);
        var caption = $"{shortHash}… · {local:dd/MM/yyyy HH:mm} Bogotá";
        var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, 6);
        gfx.DrawString(caption, font, new XSolidBrush(XColor.FromArgb(130, 130, 130)), x, y + height + 8);
    }

    /// <summary>Manual word-wrap instead of PdfSharp's XRect+XStringFormat overload — that overload
    /// did not reliably wrap (text ran off the page edge instead) after the 6.2.4 upgrade, so this
    /// measures each candidate line itself via MeasureString and is safe across whatever PdfSharp
    /// version is in play. Returns the Y position right after the last line drawn.</summary>
    private static double DrawWrappedText(XGraphics gfx, string text, XFont font, XBrush brush, double x, double y, double maxWidth, double lineHeight)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = "";
        var currentY = y;
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (gfx.MeasureString(candidate, font).Width > maxWidth && line.Length > 0)
            {
                gfx.DrawString(line, font, brush, x, currentY);
                currentY += lineHeight;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
        {
            gfx.DrawString(line, font, brush, x, currentY);
            currentY += lineHeight;
        }
        return currentY;
    }

    private static void DrawText(XGraphics gfx, string? value, double x, double y, double height)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var fontSize = Math.Clamp(height * 0.7, 6, 14);
        var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, fontSize);
        gfx.DrawString(value, font, XBrushes.Black, x, y + height * 0.8);
    }

    private static void DrawCheckbox(XGraphics gfx, string? value, double x, double y, double width, double height)
    {
        var side = Math.Min(width, height);
        gfx.DrawRectangle(XPens.Black, x, y, side, side);
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, side * 0.8);
            gfx.DrawString("X", font, XBrushes.Black, x + side * 0.15, y + side * 0.85);
        }
    }

    private static readonly byte[] QrDarkColorRgba = [0, 0, 0, 255];
    private static readonly byte[] QrLightColorRgba = [255, 255, 255, 255];

    /// <summary>Drawn once, top-right of the certificate's first page — encodes the public
    /// verification URL (see VerifyEnvelopeQuery) so anyone holding a printed/exported copy of the
    /// certificate can scan it to independently confirm the envelope's integrity, without needing
    /// an SDPP account. QRCoder is already a dependency elsewhere in the solution (Identity's TOTP
    /// enrollment QR) — same library, no new dependency risk.</summary>
    private static void DrawVerificationQrCode(XGraphics gfx, string verificationUrl, double x, double y, double size)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(verificationUrl, QRCodeGenerator.ECCLevel.Q);
        // GetGraphic(int) alone emits a 1-bit grayscale PNG, which PdfSharp 6.1.1's decoder rejects
        // ("Unsupported image format" — confirmed empirically in an isolated probe before this fix).
        // Passing explicit RGBA colors makes QRCoder emit 32-bit RGBA instead, which PdfSharp
        // handles the same as every other PNG this engine draws.
        var pngBytes = new PngByteQRCode(qrCodeData).GetGraphic(10, QrDarkColorRgba, QrLightColorRgba);

        using var imageStream = new MemoryStream(pngBytes, 0, pngBytes.Length, writable: false, publiclyVisible: true);
        using var xImage = XImage.FromStream(imageStream);
        gfx.DrawImage(xImage, x, y, size, size);
    }

    private const string SecurityLevelSdppSession = "Correo electrónico, Sesión SDPP (MFA)";
    private const string SecurityLevelEmailOtp = "Correo electrónico, código de un solo uso";

    // Same palette as the frontend's BRAND_COLORS (src/shared/theme.ts) — the certificate is the
    // one artifact that leaves the app as a standalone PDF, so it's the one place brand
    // consistency has to be hand-kept in sync rather than inherited from a shared theme file.
    // PdfSharpFontResolver only ever resolves DejaVuSans Regular (no reliable Bold face — see its
    // own doc comment), so hierarchy below is built with size/color/spacing only, never
    // XFontStyleEx.Bold.
    private static readonly XColor CertTeal = XColor.FromArgb(20, 168, 156);
    private static readonly XColor CertOrange = XColor.FromArgb(245, 130, 31);
    private static readonly XColor CertMagenta = XColor.FromArgb(214, 33, 122);
    private static readonly XColor CertTextDark = XColor.FromArgb(58, 58, 58);
    private static readonly XColor CertTextMuted = XColor.FromArgb(125, 125, 125);
    private static readonly XColor CertPanelBg = XColor.FromArgb(244, 249, 248);
    private static readonly XColor CertPanelBorder = XColor.FromArgb(210, 228, 224);
    private static readonly XColor CertCalloutBg = XColor.FromArgb(250, 250, 250);
    private static readonly XColor CertCardBorder = XColor.FromArgb(225, 225, 225);

    private const string LegalDeclaration =
        "Este documento fue suscrito mediante un proceso de firma electrónica con trazabilidad " +
        "completa: verificación de identidad de cada firmante, registro expreso de su consentimiento, " +
        "huella digital (SHA-256) individual de cada firma y del documento, y una bitácora de " +
        "auditoría con fecha, hora, dirección IP y método de autenticación de cada evento. Esta firma " +
        "electrónica constituye evidencia del consentimiento de las partes conforme a la legislación " +
        "aplicable. Aclaración: no se trata de una firma digital basada en certificados criptográficos " +
        "(PKI/X.509); su validez jurídica como firma electrónica simple/con trazabilidad debe " +
        "evaluarse conforme a la normativa vigente en la jurisdicción correspondiente.";

    private static readonly TimeZoneInfo CertificateTimeZone = ResolveCertificateTimeZone();
    private static readonly byte[]? LogoBytes = LoadAssetBytes("logo-tics.png");
    private static readonly byte[]? StampBytes = LoadAssetBytes("sello.png");

    private static TimeZoneInfo ResolveCertificateTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
        catch (TimeZoneNotFoundException)
        {
            // Fixed UTC-5, no DST — functionally identical to America/Bogota if the IANA tz data
            // isn't installed in this container for some reason (see Dockerfile's tzdata package).
            return TimeZoneInfo.CreateCustomTimeZone("UTC-05:00", TimeSpan.FromHours(-5), "Bogotá (UTC-05:00)", "Bogotá (UTC-05:00)");
        }
    }

    private static byte[]? LoadAssetBytes(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>Converts a stored UTC timestamp to the certificate's display timezone (Bogotá,
    /// UTC-05:00, no DST) with an explicit offset label — printed times must read correctly for a
    /// reader in that timezone, not silently as UTC (see this class's doc comment history).</summary>
    private static string FormatCertificateTime(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), CertificateTimeZone);
        return $"{local:yyyy-MM-dd HH:mm:ss} (UTC-05:00) Bogotá, Colombia";
    }

    /// <summary>Number of lines <see cref="DrawWrappedText"/> would produce for this text, without
    /// drawing anything — lets a panel's background box be measured and painted BEFORE its text
    /// (correct z-order), while still wrapping at the exact same width/font the real draw uses.</summary>
    private static int CountWrappedLines(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = "";
        var lines = 0;
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (gfx.MeasureString(candidate, font).Width > maxWidth && line.Length > 0)
            {
                lines++;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0) lines++;
        return lines;
    }

    /// <summary>Filled circle + centered initial — the closest a PDF-drawn "avatar" gets without an
    /// actual photo, same idea as the frontend's Avatar bubble (AppShell.tsx) for visual continuity.</summary>
    private static void DrawInitialAvatar(XGraphics gfx, string fullName, double centerX, double centerY, double diameter, XColor color)
    {
        gfx.DrawEllipse(new XSolidBrush(color), centerX - diameter / 2, centerY - diameter / 2, diameter, diameter);
        var initial = fullName.Length > 0 ? fullName[..1].ToUpperInvariant() : "?";
        var font = new XFont(PdfSharpFontResolver.DefaultFamilyName, diameter * 0.45);
        var size = gfx.MeasureString(initial, font);
        gfx.DrawString(initial, font, XBrushes.White, centerX - size.Width / 2, centerY + size.Height / 2 - size.Height * 0.12);
    }

    /// <summary>Small filled rounded pill with centered text — used for the "Completado" status and
    /// each signer's authentication level, standing in for the frontend's MUI Chip.</summary>
    private static double DrawPill(XGraphics gfx, string text, XFont font, double x, double y, XColor color, bool filled = true)
    {
        var textSize = gfx.MeasureString(text, font);
        var paddingX = 8.0;
        var height = textSize.Height + 7;
        var width = textSize.Width + paddingX * 2;
        var pen = new XPen(color, 1);
        if (filled)
        {
            gfx.DrawRoundedRectangle(new XSolidBrush(color), x, y, width, height, height, height);
            gfx.DrawString(text, font, XBrushes.White, x + paddingX, y + height / 2 + textSize.Height / 2 - textSize.Height * 0.14);
        }
        else
        {
            gfx.DrawRoundedRectangle(pen, new XSolidBrush(XColor.FromArgb(255, 255, 255)), x, y, width, height, height, height);
            gfx.DrawString(text, font, new XSolidBrush(color), x + paddingX, y + height / 2 + textSize.Height / 2 - textSize.Height * 0.14);
        }
        return width;
    }

    /// <summary>Deliberately styled after a DocuSign "Certificado de finalización", now with SDPP's
    /// own brand palette (teal/naranja/magenta) instead of plain black-on-white: a tri-color header
    /// stripe, a tinted info panel, a callout box for the legal declaration, and one card per signer
    /// (avatar initial, status pill, timeline with colored bullets, single canonical signature hash —
    /// see CertificateRecipientSummary.SignatureHash's doc comment for why it's one value now, not a
    /// list) — see AuditCertificateInfo's doc comment for why the file's own hash is never printed
    /// here. Paginates: NewPageIfNeeded starts a fresh page (repainting the header stripe) whenever a
    /// block wouldn't fit on the current one, so an envelope with many signers never truncates
    /// silently.</summary>
    private static void DrawCertificatePage(PdfDocument document, AuditCertificateInfo certificate)
    {
        const double marginX = 40;
        const double lineHeight = 15;
        const double sectionGap = 16;
        const double signatureThumbWidth = 110;
        const double signatureThumbHeight = 42;
        const double topStripeHeight = 5;
        const double topMargin = 30;
        const double cardPaddingX = 16;
        const double cardPaddingY = 14;
        const double avatarDiameter = 26;

        var titleFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 19);
        var subtitleFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 8.5);
        var headingFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 12);
        var nameFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 11.5);
        var bodyFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 9);
        var labelFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 7.5);
        var tinyFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 7);
        var legalFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 7.5);
        var pillFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 7.5);
        var eyebrowFont = new XFont(PdfSharpFontResolver.DefaultFamilyName, 7);

        var page = document.AddPage();
        var pageWidth = page.Width.Point;
        var pageHeight = page.Height.Point;
        var gfx = XGraphics.FromPdfPage(page);
        double y;

        void DrawTopStripe()
        {
            var bandWidth = pageWidth / 3.0;
            gfx.DrawRectangle(new XSolidBrush(CertTeal), 0, 0, bandWidth, topStripeHeight);
            gfx.DrawRectangle(new XSolidBrush(CertOrange), bandWidth, 0, bandWidth, topStripeHeight);
            gfx.DrawRectangle(new XSolidBrush(CertMagenta), bandWidth * 2, 0, pageWidth - bandWidth * 2, topStripeHeight);
        }

        void NewPageIfNeeded(double neededHeight)
        {
            if (y + neededHeight <= pageHeight - topMargin) return;
            gfx.Dispose();
            page = document.AddPage();
            gfx = XGraphics.FromPdfPage(page);
            DrawTopStripe();
            y = topStripeHeight + topMargin;
        }

        DrawTopStripe();
        y = topStripeHeight + topMargin;

        // --- Header: logo + title + tagline, QR code (bordered) top-right ---
        var titleX = marginX;
        if (LogoBytes is not null)
        {
            const double logoHeight = 36;
            using var logoStream = new MemoryStream(LogoBytes, 0, LogoBytes.Length, writable: false, publiclyVisible: true);
            using var logoImage = XImage.FromStream(logoStream);
            var logoWidth = logoHeight * (logoImage.PixelWidth / (double)logoImage.PixelHeight);
            gfx.DrawImage(logoImage, marginX, y, logoWidth, logoHeight);
            titleX = marginX + logoWidth + 14;
        }
        gfx.DrawString("Certificado de Finalización", titleFont, new XSolidBrush(CertTextDark), titleX, y + 22);
        gfx.DrawString("EVIDENCIA DE FIRMA ELECTRÓNICA · SDPP", subtitleFont, new XSolidBrush(CertTeal), titleX, y + 35);

        // Box width fixed first, box position derived from it (right edge pinned to the margin) —
        // drawing it the other way around (position first, width guessed) is what previously let
        // the box's right edge run past the margin, with the caption overflowing past that.
        const double qrSize = 60;
        const double qrBoxWidth = qrSize + 16;
        const double qrBoxHeight = qrSize + 28;
        var qrBoxTop = y;
        var qrBoxX = pageWidth - marginX - qrBoxWidth;
        gfx.DrawRectangle(new XPen(CertPanelBorder, 1), XBrushes.White, qrBoxX, qrBoxTop, qrBoxWidth, qrBoxHeight);
        DrawVerificationQrCode(gfx, certificate.VerificationUrl, qrBoxX + 8, qrBoxTop + 8, qrSize);
        var qrCaption = "Verificar";
        var qrCaptionSize = gfx.MeasureString(qrCaption, tinyFont);
        gfx.DrawString(qrCaption, tinyFont, new XSolidBrush(CertTextMuted), qrBoxX + (qrBoxWidth - qrCaptionSize.Width) / 2, qrBoxTop + qrSize + 22);

        // Whichever is taller wins — the title/logo block or the QR box — so the info panel drawn
        // next always starts clear of both. The QR box previously ran a few points past a hardcoded
        // "y += 58", so its bottom edge (and the last few pixels of the actual QR code inside it)
        // ended up painted over by the info panel's own background. sectionGap is real breathing
        // room, not just a rounding margin.
        y = Math.Max(y + 46, qrBoxTop + qrBoxHeight) + sectionGap;

        // --- "Completado" status pill, right-aligned in its own row above the panel — giving it a
        // row of its own (instead of sharing one with "DOCUMENTO") means it never has to coexist
        // horizontally with a value that might wrap under it. ---
        var statusText = "COMPLETADO";
        var statusTextSize = gfx.MeasureString(statusText, pillFont);
        var statusPillWidth = statusTextSize.Width + 16;
        DrawPill(gfx, statusText, pillFont, pageWidth - marginX - statusPillWidth, y, CertTeal);
        y += statusTextSize.Height + 7 + 10;

        // --- Info panel: tinted box grouping the envelope summary. Every value is drawn with
        // DrawWrappedText — never assumed to fit next to its label on one line — because a hash is
        // always 64 hex characters and a document title/URL is whatever length the user gave it;
        // that assumption is exactly what let long values run past the panel's right edge before.
        var infoRows = new (string Label, string Value)[]
        {
            ("DOCUMENTO", certificate.EnvelopeTitle),
            ("IDENTIFICADOR DEL SOBRE", certificate.EnvelopeId.ToString()),
            ("COMPLETADO", FormatCertificateTime(certificate.CompletedAtUtc)),
            ("FIRMANTES", certificate.Recipients.Count.ToString()),
            ("ALGORITMO", "ECDSA P-256 (SHA-256) — atestación de plataforma SDPP, no PKI personal"),
            ("HASH SHA-256 DEL DOCUMENTO ORIGINAL", certificate.OriginalSha256Hash),
            ("HASH SHA-256 DEL SOBRE", certificate.EnvelopeHash),
            ("VERIFICAR EN", certificate.VerificationUrl),
        };
        const double rowSpacing = 7;
        var infoContentWidth = pageWidth - 2 * marginX - cardPaddingX * 2;
        var rowLineCounts = infoRows.Select(r => Math.Max(1, CountWrappedLines(gfx, r.Value, bodyFont, infoContentWidth))).ToArray();
        var infoContentHeight = infoRows.Select((_, i) => lineHeight + rowLineCounts[i] * lineHeight + rowSpacing).Sum();
        var infoPanelHeight = cardPaddingY * 2 + infoContentHeight;
        NewPageIfNeeded(infoPanelHeight);
        gfx.DrawRectangle(new XPen(CertPanelBorder, 1), new XSolidBrush(CertPanelBg), marginX, y, pageWidth - 2 * marginX, infoPanelHeight);

        var infoY = y + cardPaddingY;
        var infoX = marginX + cardPaddingX;
        foreach (var (label, value) in infoRows)
        {
            gfx.DrawString(label, eyebrowFont, new XSolidBrush(CertTeal), infoX, infoY);
            infoY += lineHeight;
            infoY = DrawWrappedText(gfx, value, bodyFont, new XSolidBrush(CertTextDark), infoX, infoY, infoContentWidth, lineHeight);
            infoY += rowSpacing;
        }
        y += infoPanelHeight + sectionGap;

        // --- Legal declaration: neutral callout box with a colored left accent bar ---
        var legalWidth = pageWidth - 2 * marginX - cardPaddingX * 2;
        var legalLines = CountWrappedLines(gfx, LegalDeclaration, legalFont, legalWidth);
        var legalPanelHeight = cardPaddingY * 2 + legalLines * 10;
        NewPageIfNeeded(legalPanelHeight);
        gfx.DrawRectangle(new XPen(CertCardBorder, 1), new XSolidBrush(CertCalloutBg), marginX, y, pageWidth - 2 * marginX, legalPanelHeight);
        gfx.DrawRectangle(new XSolidBrush(CertOrange), marginX, y, 3, legalPanelHeight);
        DrawWrappedText(gfx, LegalDeclaration, legalFont, new XSolidBrush(CertTextMuted), marginX + cardPaddingX, y + cardPaddingY, legalWidth, 10);
        y += legalPanelHeight + sectionGap;

        // --- "Seguimiento de registro" section heading with a colored underline ---
        NewPageIfNeeded(lineHeight * 2);
        gfx.DrawString("Seguimiento de registro", headingFont, new XSolidBrush(CertTextDark), marginX, y);
        y += lineHeight * 0.7;
        gfx.DrawRectangle(new XSolidBrush(CertTeal), marginX, y, 36, 2.5);
        y += lineHeight * 0.9;

        var avatarPalette = new[] { CertTeal, CertOrange, CertMagenta };
        for (var i = 0; i < certificate.Recipients.Count; i++)
        {
            var recipient = certificate.Recipients[i];
            var securityLevel = recipient.AuthMethodUsed switch
            {
                "SdppSession" => SecurityLevelSdppSession,
                "EmailOtp" => SecurityLevelEmailOtp,
                _ => "No determinado",
            };

            // Mirrors exactly the sequence drawn below, so the reserved height is always exact
            // regardless of which optional lines (hash, cryptographic signature) are present.
            // Tall enough to clear the security-level pill drawn at avatarDiameter-4 below the row
            // start (its own bottom edge lands at roughly avatarDiameter+18) — this previously ran
            // shorter than the pill itself, which let the first timeline row overlap it.
            var headerBlockHeight = avatarDiameter + 22;
            var timelineHeight = lineHeight * 3;
            var hashLineHeight = recipient.SignatureHash is not null ? lineHeight * 0.95 : 0;
            var cryptoLineHeight = recipient.CryptographicSignatureId is not null ? lineHeight * 0.95 : 0;
            var cardContentHeight = headerBlockHeight + timelineHeight + hashLineHeight + cryptoLineHeight;
            var cardHeight = Math.Max(cardContentHeight, signatureThumbHeight + headerBlockHeight) + cardPaddingY * 2;

            NewPageIfNeeded(cardHeight + sectionGap * 0.5);

            var cardTopY = y;
            gfx.DrawRectangle(new XPen(CertCardBorder, 1), XBrushes.White, marginX, cardTopY, pageWidth - 2 * marginX, cardHeight);
            var accentColor = avatarPalette[i % avatarPalette.Length];
            gfx.DrawRectangle(new XSolidBrush(accentColor), marginX, cardTopY, 4, cardHeight);

            var contentX = marginX + cardPaddingX;
            var rowY = cardTopY + cardPaddingY;

            DrawInitialAvatar(gfx, recipient.FullName, contentX + avatarDiameter / 2, rowY + avatarDiameter / 2 - 2, avatarDiameter, accentColor);
            var nameX = contentX + avatarDiameter + 10;
            gfx.DrawString(recipient.FullName, nameFont, new XSolidBrush(CertTextDark), nameX, rowY + 6);
            gfx.DrawString(recipient.Email, labelFont, new XSolidBrush(CertTextMuted), nameX, rowY + 19);
            DrawPill(gfx, securityLevel, pillFont, nameX, rowY + avatarDiameter - 4, accentColor, filled: false);
            rowY += headerBlockHeight + 4;

            void DrawTimelineRow(string label, DateTime? at, string? ip)
            {
                var done = at is not null;
                var dotColor = done ? CertTeal : XColor.FromArgb(210, 210, 210);
                gfx.DrawEllipse(new XSolidBrush(dotColor), contentX + 1, rowY - 4, 6, 6);
                var text = done
                    ? $"{label}: {FormatCertificateTime(at!.Value)}{(ip is { } ipValue ? $" — IP {ipValue}" : "")}"
                    : $"{label}: —";
                gfx.DrawString(text, bodyFont, new XSolidBrush(done ? CertTextDark : CertTextMuted), contentX + 14, rowY);
                rowY += lineHeight;
            }

            DrawTimelineRow("Enviado", recipient.SentAtUtc, null);
            DrawTimelineRow("Visto", recipient.ViewedAtUtc, recipient.ViewedIpAddress);
            DrawTimelineRow("Firmado", recipient.SignedAtUtc, recipient.SignedIpAddress);

            if (recipient.SignatureHash is { } hash)
            {
                gfx.DrawString($"Hash de firma: {hash}", tinyFont, new XSolidBrush(CertTextMuted), contentX, rowY);
                rowY += lineHeight * 0.95;
            }
            if (recipient.CryptographicSignatureId is { } signatureId && recipient.CryptographicAlgorithm is { } algorithm)
            {
                gfx.DrawString($"Firma criptográfica: {signatureId} — {algorithm}", tinyFont, new XSolidBrush(CertTextMuted), contentX, rowY);
            }

            // The signer's own signature image, top-right of their card — same "contain, never
            // distort" fit used when embedding it on the document itself.
            if (recipient.SignatureImage is { Length: > 0 })
            {
                var thumbX = pageWidth - marginX - cardPaddingX - signatureThumbWidth;
                var thumbY = cardTopY + cardPaddingY;
                gfx.DrawRectangle(new XPen(CertCardBorder, 1), XBrushes.White, thumbX - 3, thumbY - 3, signatureThumbWidth + 6, signatureThumbHeight + 6);
                DrawImage(gfx, recipient.SignatureImage, thumbX, thumbY, signatureThumbWidth, signatureThumbHeight);
            }

            y = cardTopY + cardHeight + sectionGap * 0.6;
        }

        gfx.Dispose();
    }
}
