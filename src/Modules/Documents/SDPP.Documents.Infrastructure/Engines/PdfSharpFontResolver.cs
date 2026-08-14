using PdfSharp.Fonts;

namespace SDPP.Documents.Infrastructure.Engines;

/// <summary>
/// PdfSharp 6 has no GDI+/system font access on Linux and throws unless a resolver is
/// registered. The worker image already carries fonts-dejavu-core as a LibreOffice dependency
/// (see Dockerfile), so every requested family resolves to that single, always-present face
/// rather than depending on font names matching whatever's installed.
/// </summary>
public sealed class PdfSharpFontResolver : IFontResolver
{
    private const string FaceName = "DejaVuSans";

    /// <summary>The only family name this resolver actually has — every XFont in this codebase
    /// should be constructed with this, since any other name still resolves to the same face
    /// (see ResolveTypeface) but would read as misleading in calling code.</summary>
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
