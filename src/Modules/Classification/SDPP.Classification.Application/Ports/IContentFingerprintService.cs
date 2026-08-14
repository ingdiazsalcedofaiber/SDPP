namespace SDPP.Classification.Application.Ports;

public sealed record FingerprintResult(string ContentFingerprint, string StructuralSignature);

/// <summary>
/// Computes a fingerprint over a document's normalized extracted text — not its bytes — so the
/// same logical content still fingerprints the same after a format conversion (see the
/// integrity-and-fingerprint proposal, "Normalización y algoritmo de fingerprint"). Moved here
/// from the Documents module as part of the "Clasificación de Activos de Información" extraction.
/// <see cref="FingerprintResult.ContentFingerprint"/> is an exact-match SHA-256;
/// <see cref="FingerprintResult.StructuralSignature"/> is a 64-bit SimHash used as a near-duplicate
/// fallback when extraction introduces noise (OCR, ligatures) between two conversions of the same
/// content — see IChangeDetectionService.
/// </summary>
public interface IContentFingerprintService
{
    FingerprintResult Compute(string extractedText);

    /// <summary>Hamming distance (0-64) between two StructuralSignature hex values — the input to
    /// IChangeDetectionService's near-duplicate/partial/total thresholds.</summary>
    int HammingDistance(string structuralSignatureA, string structuralSignatureB);
}
