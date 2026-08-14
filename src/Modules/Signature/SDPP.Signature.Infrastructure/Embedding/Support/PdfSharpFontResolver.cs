using PdfSharp.Fonts;

namespace SDPP.Signature.Infrastructure.Embedding.Support;

/// <summary>
/// PdfSharp 6 has no GDI+/system font access on Linux and throws unless a resolver is registered.
/// Duplicated (not shared) from SDPP.Classification.Infrastructure.Protection.Support's copy of the
/// same class — that module explicitly can't be referenced from here (each module's Infrastructure
/// stays unreferenced by any other, even at the cost of duplicating this ~40-line utility a third
/// time; see that copy's own doc comment for the same reasoning against Documents.Infrastructure's
/// original).
/// </summary>
public sealed class PdfSharpFontResolver : IFontResolver
{
    private const string FaceName = "DejaVuSans";

    public const string DefaultFamilyName = FaceName;

    private static readonly string FontPath = ResolveFontPath();

    public string DefaultFontName => FaceName;

    public byte[] GetFont(string faceName) => File.ReadAllBytes(FontPath);

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) => new(FaceName);

    private static string ResolveFontPath()
    {
        string[] candidates =
        [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/dejavu/DejaVuSans.ttf",
        ];
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("No se encontró la fuente DejaVu Sans requerida por PdfSharp.");
    }
}
