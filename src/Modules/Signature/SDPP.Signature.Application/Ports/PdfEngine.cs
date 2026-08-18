using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.Ports;

/// <summary>One EnvelopeField, with its recipient's identity resolved, ready to be drawn — the
/// Application layer flattens the aggregate's Fields+Recipients into this before calling the
/// engine, so Infrastructure never needs to know about the domain model.</summary>
public sealed record ResolvedField(
    FieldType Type, int PageNumber, double PositionX, double PositionY, double Width, double Height,
    string? Value, byte[]? SignatureImage, string? SignatureHash, DateTime? FilledAtUtc, string RecipientFullName);

/// <summary>Everything the certificate page prints for one signer — mirrors the level of detail a
/// DocuSign "Certificado de finalización" shows per recipient (seguimiento de registro: enviado/
/// visto/firmado con IP y método de autenticación en cada paso).</summary>
public sealed record CertificateRecipientSummary(
    string FullName, string Email, string? AuthMethodUsed,
    DateTime? SentAtUtc, DateTime? ViewedAtUtc, string? ViewedIpAddress, DateTime? SignedAtUtc, string? SignedIpAddress,
    // Singular — the ONE representative field's hash (same field as SignatureImage below), matching
    // the short hash caption stamped on the document page directly under that signature/initials
    // image (see PdfSharpEnvelopeEmbeddingEngine.DrawSignatureCaption) — deliberately NOT
    // DocumentSignature.CanonicalPayloadHash, which folds the field hash together with identity/
    // consent/timestamp context into a different value that would never match what's on the page.
    // A recipient with more than one field (e.g. Firma + the Gerencia Legal stamp) still only gets
    // one line here — see CompleteRecipientSigningCommand's representativeField selection.
    string? SignatureHash, byte[]? SignatureImage,
    Guid? CryptographicSignatureId, string? CryptographicAlgorithm);

/// <summary>Everything needed to append the "Certificado de Auditoría" page (spec point 1) —
/// deliberately does NOT include a hash of the final PDF file itself, which would be self-
/// referential (the file isn't finished being written until this page is drawn); OriginalSha256Hash
/// (the source PDF, before any signing) is shown, plus EnvelopeHash — a hash over the fully-signed
/// recipient/field state (SignatureEnvelope.PreviewEnvelopeHash), which IS safe to compute and print
/// here because it depends only on that state, not on the bytes of the file currently being
/// written. The final file's own hash is computed by the caller afterward and only ever stored/
/// queryable via the API and audit trail, never printed inside the PDF.</summary>
public sealed record AuditCertificateInfo(
    string EnvelopeTitle, Guid EnvelopeId, DateTime CompletedAtUtc, string OriginalSha256Hash, string EnvelopeHash,
    string VerificationUrl, IReadOnlyList<CertificateRecipientSummary> Recipients);

/// <returns>
/// <paramref name="CombinedPdfPath"/> is the source document with every field embedded, plus (when a
/// certificate was requested) the certificate pages appended — this is what becomes the locked,
/// versioned signed document. <paramref name="CertificatePdfPath"/> is that SAME certificate content
/// as its own standalone file (null when no certificate was requested) — produced by a page-copy-
/// only split off the already-saved combined file (no XGraphics/XFont involved in the split itself),
/// which is what keeps it safe to do in the same process right after the font-using combined-file
/// generation (see PdfSharpEnvelopeEmbeddingEngine's doc comment for the crash this sidesteps).
/// </returns>
public sealed record EmbedResult(string CombinedPdfPath, string? CertificatePdfPath);

/// <summary>
/// Draws every resolved field of every recipient onto the source PDF, and — when the envelope is
/// completing — appends the audit certificate page, all within a single PdfSharp document session.
/// Deliberately one call instead of two separate open/save cycles: PdfSharp's global font-resolver
/// cache doesn't tolerate two independent PdfDocument instances using fonts in the same process (see
/// PdfSharpEnvelopeEmbeddingEngine's doc comment for the crash this caused when they were separate).
/// Not a PDF editor: never touches existing text, never adds/removes pages beyond the certificate
/// page(s).
/// </summary>
public interface IPdfEnvelopeEmbeddingEngine
{
    EmbedResult Embed(string inputPdfPath, IReadOnlyList<ResolvedField> fields, AuditCertificateInfo? certificate = null);
}
