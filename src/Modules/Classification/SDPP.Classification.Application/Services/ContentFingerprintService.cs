using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Application.Services;

/// <summary>
/// See the integrity-and-fingerprint proposal, "Normalización y algoritmo de fingerprint". Two
/// signals, both computed over the same normalized text: an exact-match SHA-256
/// (ContentFingerprint) and a 64-bit SimHash over 5-word shingles (StructuralSignature) used as a
/// near-duplicate fallback when extraction noise (OCR, ligatures) keeps two conversions of the
/// same content from hashing identically.
/// </summary>
public sealed partial class ContentFingerprintService : IContentFingerprintService
{
    private const int ShingleSize = 5;

    public FingerprintResult Compute(string extractedText)
    {
        var normalized = Normalize(extractedText);
        var contentFingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        var structuralSignature = ComputeSimHash(normalized);
        return new FingerprintResult(contentFingerprint, structuralSignature);
    }

    public int HammingDistance(string structuralSignatureA, string structuralSignatureB)
    {
        var a = ulong.Parse(structuralSignatureA, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = ulong.Parse(structuralSignatureB, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return BitOperations.PopCount(a ^ b);
    }

    /// <summary>Unicode NFKC (collapses compatibility variants an evasion attempt might rely on) +
    /// strip of invisible/control characters + whitespace collapse + lowercase — the goal is that
    /// extracting a Word document and extracting the PDF it was converted to (no real edit in
    /// between) produce the exact same normalized text.</summary>
    private static string Normalize(string text)
    {
        var nfkc = text.Normalize(NormalizationForm.FormKC);
        var noControls = ControlCharsRegex().Replace(nfkc, " ");
        var collapsed = WhitespaceRegex().Replace(noControls, " ").Trim();
        return collapsed.ToLowerInvariant();
    }

    private static string ComputeSimHash(string normalizedText)
    {
        var words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var vector = new int[64];

        if (words.Length < ShingleSize)
        {
            // Too short to shingle meaningfully — hash the whole thing as a single "shingle" so
            // very short documents still get a stable, comparable signature instead of all-zero.
            AccumulateShingle(vector, normalizedText);
        }
        else
        {
            for (var i = 0; i <= words.Length - ShingleSize; i++)
            {
                AccumulateShingle(vector, string.Join(' ', words, i, ShingleSize));
            }
        }

        var signature = 0UL;
        for (var bit = 0; bit < 64; bit++)
        {
            if (vector[bit] > 0)
            {
                signature |= 1UL << bit;
            }
        }

        return signature.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static void AccumulateShingle(int[] vector, string shingle)
    {
        var hash = Fnv1a64(shingle);
        for (var bit = 0; bit < 64; bit++)
        {
            vector[bit] += (hash & (1UL << bit)) != 0 ? 1 : -1;
        }
    }

    /// <summary>FNV-1a 64-bit — deterministic across processes/restarts, unlike
    /// <see cref="string.GetHashCode()"/>, which .NET randomizes per-process and would make
    /// fingerprints incomparable between two different service instances.</summary>
    private static ulong Fnv1a64(string value)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }

    [GeneratedRegex(@"[\p{Cc}\p{Cf}]")]
    private static partial Regex ControlCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
