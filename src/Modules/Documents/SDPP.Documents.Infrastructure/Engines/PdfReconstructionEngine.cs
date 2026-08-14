using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// Builds real, valid OOXML files directly (DocumentFormat.OpenXml — Microsoft's own SDK, byte-
/// exact with what Word/Excel/PowerPoint themselves write) instead of asking LibreOffice to
/// import a PDF and re-export it as Office, which LibreOfficeEngine's comment explains is
/// unreliable: LibreOffice's PDF import is fundamentally a Draw/graphics import, incompatible
/// with the Writer/Calc/Impress export filters.
///
/// Honest scope: a PDF has no structural concept of "paragraph" or "spreadsheet cell" — only
/// positioned glyphs, so no PDF→Office conversion (by this or any other tool, including Adobe's
/// own) can be byte-identical to the source. PdfToWord uses `pdftotext -bbox-layout` (word-level
/// bounding boxes, not just raw text) to reconstruct real paragraphs AND real OOXML tables — see
/// ExtractStructuredPagesAsync/BuildBodyElements — which is a large step up from dumping one
/// paragraph per line of `-layout` text, though fonts/colors/images are still not preserved.
/// PdfToExcel keeps the simpler `-layout` + whitespace-column heuristic (spreadsheets tolerate a
/// rougher grid better than a document tolerates losing its table borders entirely). PdfToPpt
/// instead rasterizes each page and places one full-page image per slide, which *is* visually
/// faithful (a slide is naturally page-shaped) — the only lossy direction is text selectability,
/// which was never a requirement for slide decks.
/// </summary>
public sealed class PdfReconstructionEngine(ILogger<PdfReconstructionEngine> logger) : IConversionEngine
{
    private static readonly HashSet<OperationType> SupportedOperations =
    [
        OperationType.PdfToWord, OperationType.PdfToExcel, OperationType.PdfToPpt,
    ];

    public bool CanHandle(OperationType operationType) => SupportedOperations.Contains(operationType);

    public async Task<ConversionEngineResult> ConvertAsync(
        IReadOnlyList<string> inputFilePaths, OperationType operationType, IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        var inputFilePath = inputFilePaths[0];
        return operationType switch
        {
            OperationType.PdfToWord => await ToWordAsync(inputFilePath, cancellationToken),
            OperationType.PdfToExcel => await ToExcelAsync(inputFilePath, cancellationToken),
            OperationType.PdfToPpt => await ToPptAsync(inputFilePath, cancellationToken),
            _ => throw new NotSupportedException($"PdfReconstructionEngine no maneja '{operationType}'."),
        };
    }

    private async Task<ConversionEngineResult> ToWordAsync(string inputFilePath, CancellationToken ct)
    {
        List<List<TextLine>>? structuredPages = null;
        try
        {
            structuredPages = await ExtractStructuredPagesAsync(inputFilePath, ct);
        }
        catch (Exception ex)
        {
            // Any failure in the position-aware path (pdftotext -bbox-layout erroring on an
            // unusual PDF, malformed XML output, etc.) degrades to the old plain-text extraction
            // rather than failing the whole conversion — never worse than before this change.
            logger.LogWarning(ex, "No se pudo extraer la estructura posicional del PDF; se usará extracción de texto simple.");
        }

        List<string>? fallbackLines = null;
        if (structuredPages is null || structuredPages.All(p => p.Count == 0))
        {
            var textResult = await ExtractLayoutTextAsync(inputFilePath, ct);
            if (textResult.ErrorDetail is not null)
            {
                return new ConversionEngineResult(false, null, "OpenXML", textResult.ErrorDetail);
            }
            fallbackLines = textResult.Lines;
        }

        var outputPath = NewTempFile(".docx");
        using (var document = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            if (fallbackLines is not null)
            {
                foreach (var line in fallbackLines)
                {
                    // Fully qualified: DocumentFormat.OpenXml.Spreadsheet also defines Run/Text,
                    // and both namespaces are open in this file (Spreadsheet is used in
                    // ToExcelAsync), so the unqualified names are ambiguous here.
                    body.AppendChild(new Paragraph(new W.Run(new W.Text(line) { Space = SpaceProcessingModeValues.Preserve })));
                }
            }
            else
            {
                for (var pageIndex = 0; pageIndex < structuredPages!.Count; pageIndex++)
                {
                    foreach (var element in BuildBodyElements(structuredPages[pageIndex]))
                    {
                        body.AppendChild(element);
                    }
                    if (pageIndex < structuredPages.Count - 1)
                    {
                        body.AppendChild(new Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
                    }
                }
            }

            if (!body.HasChildren)
            {
                body.AppendChild(new Paragraph(new W.Run(new W.Text(string.Empty))));
            }

            body.AppendChild(new SectionProperties());
            mainPart.Document.Save();
        }

        return new ConversionEngineResult(true, outputPath, "OpenXML", null);
    }

    /// <summary>One word, kept separately from its line's concatenated text so table reconstruction
    /// can re-bucket individual words into columns — see TryBuildTable's word-level assignment.</summary>
    private sealed record TextWord(double XMin, double XMax, string Text);

    /// <summary>One line of text on a page, with its bounding box in PDF points. The atomic unit
    /// for both table reconstruction and paragraph reconstruction — see BuildBodyElements. Using
    /// individual lines (not poppler's own block/flow grouping) is what makes dense data tables
    /// work: poppler frequently merges an entire multi-row table into one <c>&lt;block&gt;</c>
    /// (no per-cell boundaries at all) whenever rows are packed tightly, so grouping by block —
    /// the previous implementation — never even saw the row/column structure to reconstruct.</summary>
    private sealed record TextLine(double XMin, double YMin, double XMax, double YMax, string Text, IReadOnlyList<TextWord> Words);

    /// <summary>Runs `pdftotext -bbox-layout` (word/line bounding boxes, not just raw text — see
    /// the poppler CLI docs) and parses the resulting XHTML into per-page lists of
    /// <see cref="TextLine"/>, sorted in reading order (top-to-bottom, then left-to-right).</summary>
    private static async Task<List<List<TextLine>>> ExtractStructuredPagesAsync(string inputFilePath, CancellationToken ct)
    {
        var (exitCode, stdOut, stdErr) = await ProcessRunner.RunAsync(
            "pdftotext", ["-bbox-layout", inputFilePath, "-"], TimeSpan.FromMinutes(3), ct);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"pdftotext -bbox-layout terminó con código {exitCode}: {stdErr}");
        }

        using var reader = XmlReader.Create(new StringReader(stdOut), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        var doc = XDocument.Load(reader);

        // Ignore the XHTML namespace rather than matching it exactly — poppler has changed its
        // declared xmlns across versions, and LocalName matching is immune to that.
        return doc.Descendants().Where(e => e.Name.LocalName == "page").Select(ParsePage).ToList();
    }

    private static List<TextLine> ParsePage(XElement pageElement)
    {
        var lines = new List<TextLine>();
        // Poppler nests lines several levels below <page> (page > flow > block > line > word) —
        // Descendants() finds every <line> regardless of how many <flow>/<block> levels poppler
        // puts in between, which varies by document.
        foreach (var lineEl in pageElement.Descendants().Where(e => e.Name.LocalName == "line"))
        {
            var wordEls = lineEl.Elements().Where(e => e.Name.LocalName == "word").ToList();
            if (wordEls.Count == 0)
            {
                continue;
            }

            var words = wordEls
                .Select(w => new TextWord((double)w.Attribute("xMin")!, (double)w.Attribute("xMax")!, SanitizeForXml(w.Value)))
                .ToList();

            lines.Add(new TextLine(
                (double)lineEl.Attribute("xMin")!, (double)lineEl.Attribute("yMin")!,
                (double)lineEl.Attribute("xMax")!, (double)lineEl.Attribute("yMax")!,
                SanitizeForXml(string.Join(" ", words.Select(w => w.Text))), words));
        }
        return lines.OrderBy(l => l.YMin).ThenBy(l => l.XMin).ToList();
    }

    /// <summary>
    /// Turns one page's lines into real OOXML. Lines are first bucketed into visual bands (lines
    /// whose Y-ranges overlap — a table row's cells, or a heading's wrapped words, share a Y
    /// range). A band with 2+ columns starts a candidate table region, which then grows to
    /// include every following band whose lines still fall within the table's overall X range —
    /// this is what survives a wrapped cell (its continuation line is a lone, single-column band
    /// that a strict "this band alone has 2+ columns" check would otherwise treat as the end of
    /// the table). Everything else becomes a paragraph, merging wrapped single-column lines back
    /// into one run of text.
    /// </summary>
    private static List<OpenXmlElement> BuildBodyElements(List<TextLine> lines)
    {
        var elements = new List<OpenXmlElement>();
        if (lines.Count == 0)
        {
            return elements;
        }

        var bands = new List<List<TextLine>>();
        foreach (var line in lines) // already sorted in reading order
        {
            var band = bands.Count > 0 && VerticalOverlapFraction(bands[^1][0], line) > 0.3 ? bands[^1] : null;
            if (band is null)
            {
                bands.Add([line]);
            }
            else
            {
                band.Add(line);
            }
        }
        foreach (var band in bands)
        {
            band.Sort((a, b) => a.XMin.CompareTo(b.XMin));
        }

        var i = 0;
        while (i < bands.Count)
        {
            if (bands[i].Count >= 2)
            {
                var (table, bandsConsumed) = TryBuildTable(bands, i);
                if (table is not null)
                {
                    elements.Add(table);
                    i += bandsConsumed;
                    continue;
                }
            }

            // Plain paragraph: merge this band with following single-column bands that sit close
            // enough vertically to be the same wrapped paragraph rather than a new one.
            var textParts = new List<string> { string.Join(" ", bands[i].Select(l => l.Text)) };
            var last = bands[i][0];
            var j = i + 1;
            while (j < bands.Count && bands[j].Count == 1 && bands[j][0].YMin - last.YMax < (last.YMax - last.YMin) * 1.6)
            {
                textParts.Add(bands[j][0].Text);
                last = bands[j][0];
                j++;
            }

            elements.Add(new Paragraph(new W.Run(new W.Text(string.Join(" ", textParts)) { Space = SpaceProcessingModeValues.Preserve })));
            i = j;
        }

        return elements;
    }

    private static double VerticalOverlapFraction(TextLine a, TextLine b)
    {
        var overlap = Math.Min(a.YMax, b.YMax) - Math.Max(a.YMin, b.YMin);
        if (overlap <= 0)
        {
            return 0;
        }
        var minHeight = Math.Min(a.YMax - a.YMin, b.YMax - b.YMin);
        return minHeight <= 0 ? 0 : overlap / minHeight;
    }

    /// <summary>
    /// Builds a table starting at <paramref name="bands"/>[startIndex], which must already have
    /// 2+ columns. The widest of the first few bands anchors the column X-ranges (usually the
    /// header — header cells rarely wrap). The region then grows through every following band
    /// whose lines all fall within that column range, which is what keeps a wrapped cell's
    /// continuation line (a lone, single-column band) inside the table instead of ending it.
    /// Row boundaries can't just be "each band is a row" — a cell that wraps to 2 lines puts its
    /// continuation on its own single-column band, which would otherwise read as an extra row.
    /// Instead, the left-most column (rarely wrapped — usually a short id/code/number) anchors
    /// where each real row starts; every other column's lines are folded into whichever row's
    /// Y-range they land in.
    /// </summary>
    private static (W.Table? Table, int BandsConsumed) TryBuildTable(List<List<TextLine>> bands, int startIndex)
    {
        var headerBand = bands[startIndex];
        for (var k = startIndex + 1; k < Math.Min(bands.Count, startIndex + 3); k++)
        {
            if (bands[k].Count > headerBand.Count)
            {
                headerBand = bands[k];
            }
        }
        if (headerBand.Count < 2)
        {
            return (null, 0);
        }

        var columnBounds = headerBand.Select(l => (l.XMin, l.XMax)).OrderBy(b => b.XMin).ToList();
        var tableLeft = columnBounds[0].XMin - 10;
        var tableRight = columnBounds[^1].XMax + 10;

        // Only where a line STARTS has to fall inside the table's column span — where it ENDS
        // naturally varies with how long that particular cell's text is (e.g. "Resolución" runs
        // wider than "Fecha de" in the very same column), so requiring xMax to fit too was
        // cutting the region short right at the header, the row most likely to have short words
        // that undersell how wide their column really gets in the body below.
        var end = startIndex;
        while (end < bands.Count && bands[end].All(l => l.XMin >= tableLeft && l.XMin <= tableRight))
        {
            end++;
        }
        if (end - startIndex < 2)
        {
            return (null, 0); // a single band isn't enough to call this a real table
        }

        var columnCenters = columnBounds.Select(b => (b.XMin + b.XMax) / 2).ToList();

        // Assign WORDS, not whole lines, to columns — poppler sometimes keeps two adjacent
        // columns' text on the same <line> when there isn't much of a gap between them in a
        // given row (e.g. a short "vector" value right next to a date column), which would
        // otherwise dump both columns' text into whichever single column that line's overall
        // center happened to land closest to.
        var columnWords = columnCenters.Select(_ => new List<(double LineY, TextWord Word)>()).ToList();
        var anchorLineYs = new List<double>();
        foreach (var line in bands.Skip(startIndex).Take(end - startIndex).SelectMany(b => b).OrderBy(l => l.YMin))
        {
            foreach (var word in line.Words)
            {
                columnWords[ClosestColumnIndex(columnCenters, (word.XMin + word.XMax) / 2)].Add((line.YMin, word));
            }
        }

        // Anchor column = left-most column that actually has content — short id/code columns are
        // conventionally first and are the least likely to wrap across multiple lines.
        var anchorIndex = columnWords.FindIndex(c => c.Count > 0);
        if (anchorIndex < 0)
        {
            return (null, 0);
        }
        var rowStarts = columnWords[anchorIndex].Select(w => w.LineY).Distinct().OrderBy(y => y).ToList();

        var table = new W.Table(
            new W.TableProperties(
                new W.TableBorders(
                    new W.TopBorder { Val = BorderValues.Single, Size = 4 },
                    new W.BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new W.LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new W.RightBorder { Val = BorderValues.Single, Size = 4 },
                    new W.InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new W.InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
                new W.TableWidth { Type = TableWidthUnitValues.Auto }));

        for (var r = 0; r < rowStarts.Count; r++)
        {
            var rowStart = rowStarts[r] - 2;
            var rowEnd = r + 1 < rowStarts.Count ? rowStarts[r + 1] - 2 : double.MaxValue;

            var tableRow = new W.TableRow();
            foreach (var column in columnWords)
            {
                var cellText = string.Join(" ", column
                    .Where(w => w.LineY >= rowStart && w.LineY < rowEnd)
                    .OrderBy(w => w.LineY).ThenBy(w => w.Word.XMin)
                    .Select(w => w.Word.Text));
                tableRow.AppendChild(new W.TableCell(
                    new Paragraph(new W.Run(new W.Text(cellText) { Space = SpaceProcessingModeValues.Preserve }))));
            }
            table.AppendChild(tableRow);
        }

        return (table, end - startIndex);
    }

    private static int ClosestColumnIndex(List<double> columnCenters, double x)
    {
        var bestIndex = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < columnCenters.Count; i++)
        {
            var distance = Math.Abs(columnCenters[i] - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private async Task<ConversionEngineResult> ToExcelAsync(string inputFilePath, CancellationToken ct)
    {
        var textResult = await ExtractLayoutTextAsync(inputFilePath, ct);
        if (textResult.ErrorDetail is not null)
        {
            return new ConversionEngineResult(false, null, "OpenXML", textResult.ErrorDetail);
        }

        var outputPath = NewTempFile(".xlsx");
        using (var document = SpreadsheetDocument.Create(outputPath, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Datos extraídos",
            });

            uint rowIndex = 1;
            foreach (var line in textResult.Lines)
            {
                // Naive but common heuristic for "table cells" when a PDF has no real table
                // structure to begin with: runs of 2+ spaces are column separators (this is what
                // `pdftotext -layout` produces for anything that renders as visually columnar).
                var cells = System.Text.RegularExpressions.Regex.Split(line, @" {2,}")
                    .Where(c => c.Length > 0)
                    .ToList();

                var row = new Row { RowIndex = rowIndex };
                foreach (var cellValue in cells)
                {
                    row.Append(new Cell
                    {
                        CellValue = new CellValue(cellValue),
                        DataType = CellValues.String,
                    });
                }
                sheetData.Append(row);
                rowIndex++;
            }

            workbookPart.Workbook.Save();
        }

        return new ConversionEngineResult(true, outputPath, "OpenXML", null);
    }

    private async Task<ConversionEngineResult> ToPptAsync(string inputFilePath, CancellationToken ct)
    {
        var rasterDirectory = Path.Combine(Path.GetTempPath(), $"sdpp-pages-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rasterDirectory);
        var pagePrefix = Path.Combine(rasterDirectory, "page");

        var (exitCode, _, stdErr) = await ProcessRunner.RunAsync(
            "pdftoppm", ["-png", "-r", "150", inputFilePath, pagePrefix], TimeSpan.FromMinutes(5), ct);
        if (exitCode != 0)
        {
            logger.LogWarning("pdftoppm terminó con código {ExitCode}: {StdErr}", exitCode, stdErr);
            return new ConversionEngineResult(false, null, "OpenXML", stdErr);
        }

        var pageImages = Directory.GetFiles(rasterDirectory, "*.png").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (pageImages.Count == 0)
        {
            return new ConversionEngineResult(false, null, "OpenXML", "No se pudo rasterizar ninguna página del PDF.");
        }

        var outputPath = NewTempFile(".pptx");
        BuildPresentation(outputPath, pageImages);

        return new ConversionEngineResult(true, outputPath, "OpenXML", null);
    }

    private static void BuildPresentation(string outputPath, IReadOnlyList<string> pageImagePaths)
    {
        const long slideWidth = 12192000; // 16:9 widescreen, EMUs
        const long slideHeight = 6858000;

        using var document = PresentationDocument.Create(outputPath, PresentationDocumentType.Presentation);
        var presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();

        slideLayoutPart.SlideLayout = new P.SlideLayout(EmptyShapeTree()) { Type = P.SlideLayoutValues.Blank };

        slideMasterPart.SlideMaster = new P.SlideMaster(
            EmptyShapeTree(),
            new P.ColorMap
            {
                Background1 = D.ColorSchemeIndexValues.Light1,
                Text1 = D.ColorSchemeIndexValues.Dark1,
                Background2 = D.ColorSchemeIndexValues.Light2,
                Text2 = D.ColorSchemeIndexValues.Dark2,
                Accent1 = D.ColorSchemeIndexValues.Accent1,
                Accent2 = D.ColorSchemeIndexValues.Accent2,
                Accent3 = D.ColorSchemeIndexValues.Accent3,
                Accent4 = D.ColorSchemeIndexValues.Accent4,
                Accent5 = D.ColorSchemeIndexValues.Accent5,
                Accent6 = D.ColorSchemeIndexValues.Accent6,
                Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
            },
            new P.SlideLayoutIdList(new P.SlideLayoutId
            {
                Id = 2147483649,
                RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart),
            }));

        presentationPart.Presentation.Append(new P.SlideMasterIdList(new P.SlideMasterId
        {
            Id = 2147483648,
            RelationshipId = presentationPart.GetIdOfPart(slideMasterPart),
        }));

        var slideIdList = new P.SlideIdList();
        presentationPart.Presentation.Append(slideIdList);
        presentationPart.Presentation.Append(new P.SlideSize { Cx = (Int32Value)slideWidth, Cy = (Int32Value)slideHeight });
        presentationPart.Presentation.Append(new P.NotesSize { Cx = 6858000, Cy = 9144000 });

        uint slideId = 256;
        foreach (var imagePath in pageImagePaths)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            var imagePart = slidePart.AddImagePart(ImagePartType.Png);
            using (var stream = File.OpenRead(imagePath))
            {
                imagePart.FeedData(stream);
            }
            var imageRelId = slidePart.GetIdOfPart(imagePart);

            slidePart.AddPart(slideLayoutPart);

            slidePart.Slide = new P.Slide(
                new P.CommonSlideData(new P.ShapeTree(
                    NonVisualGroupProperties(),
                    new P.GroupShapeProperties(new D.TransformGroup()),
                    new P.Picture(
                        new P.NonVisualPictureProperties(
                            new P.NonVisualDrawingProperties { Id = 2, Name = "Página" },
                            new P.NonVisualPictureDrawingProperties(new D.PictureLocks { NoChangeAspect = true }),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.BlipFill(
                            new D.Blip { Embed = imageRelId },
                            new D.Stretch(new D.FillRectangle())),
                        new P.ShapeProperties(
                            new D.Transform2D(
                                new D.Offset { X = 0, Y = 0 },
                                new D.Extents { Cx = slideWidth, Cy = slideHeight }),
                            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle })))),
                new P.ColorMapOverride(new D.MasterColorMapping()));

            slideIdList.Append(new P.SlideId { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            slideId++;
        }

        presentationPart.Presentation.Save();
    }

    private static P.CommonSlideData EmptyShapeTree() => new(new P.ShapeTree(
        NonVisualGroupProperties(),
        new P.GroupShapeProperties(new D.TransformGroup())));

    private static P.NonVisualGroupShapeProperties NonVisualGroupProperties() => new(
        new P.NonVisualDrawingProperties { Id = 1, Name = string.Empty },
        new P.NonVisualGroupShapeDrawingProperties(),
        new P.ApplicationNonVisualDrawingProperties());

    private static async Task<(List<string> Lines, string? ErrorDetail)> ExtractLayoutTextAsync(string inputFilePath, CancellationToken ct)
    {
        var (exitCode, stdOut, stdErr) = await ProcessRunner.RunAsync(
            "pdftotext", ["-layout", inputFilePath, "-"], TimeSpan.FromMinutes(3), ct);

        if (exitCode != 0)
        {
            return ([], stdErr);
        }

        // pdftotext inserts a form-feed (0x0C) between pages, and XML 1.0 flatly disallows most
        // C0 control characters in text content (only tab/CR/LF are legal) — left in, this makes
        // every multi-page PDF fail with "is an invalid character" the moment OpenXML serializes
        // it. A form-feed is a page boundary, which is a paragraph break in the reconstructed
        // document anyway, so mapping it to a line break is both safe and semantically right.
        var lines = stdOut
            .Replace("\r\n", "\n")
            .Replace("\f", "\n")
            .Split('\n')
            .Select(SanitizeForXml)
            .ToList();
        return (lines, null);
    }

    /// <summary>Strips every character XML 1.0 doesn't allow in text content (C0 controls other
    /// than tab/CR/LF) so arbitrary PDF text can never break OpenXML serialization.</summary>
    private static string SanitizeForXml(string text)
    {
        Span<char> buffer = text.Length <= 256 ? stackalloc char[text.Length] : new char[text.Length];
        var written = 0;
        foreach (var c in text)
        {
            if (c is '\t' or '\r' or '\n' || c >= 0x20)
            {
                buffer[written++] = c;
            }
        }
        return new string(buffer[..written]);
    }

    private static string NewTempFile(string extension) =>
        Path.Combine(Path.GetTempPath(), $"sdpp-out-{Guid.NewGuid():N}{extension}");
}
