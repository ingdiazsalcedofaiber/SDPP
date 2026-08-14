using System.Security.Cryptography;
using System.Text;
using SDPP.BuildingBlocks.Domain;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Domain.Aggregates;

/// <summary>
/// A multi-signer signing request — replaces the old single-signer, single-shot SignatureRecord.
/// Owns its recipients and fields (same "aggregate owns child entities" shape as
/// DocumentInstance/ConversionJob); every field's value/image lives here as the durable source of
/// truth while signing is in progress, and only gets embedded into real PDF bytes once — at
/// completion, by the Application handler that calls IPdfEnvelopeEmbeddingEngine after the last
/// recipient signs (see CompleteWithFinalDocument). Nothing here talks to Documents/Identity/blob
/// storage directly — those are Application-layer concerns reached through ports.
/// </summary>
public sealed class SignatureEnvelope : AggregateRoot<Guid>
{
    private readonly List<EnvelopeRecipient> _recipients = [];
    private readonly List<EnvelopeField> _fields = [];
    private readonly List<DocumentSignature> _documentSignatures = [];
    private readonly List<ConsentRecord> _consentRecords = [];

    /// <summary>Isolation boundary for the Signature module only (see IOrganizationContextProvider's
    /// doc comment) — every backend query that returns/manages an envelope filters on this, never
    /// trusting the frontend alone. Fixed to a single default value today, since Identity has no
    /// concept of organizations yet; the column and the filtering logic are ready for when it does.</summary>
    public Guid OrganizationId { get; private set; }

    public Guid SourceDocumentId { get; private set; }
    public Guid SourceDocumentVersionId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Message { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public SigningMode SigningMode { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public EnvelopeStatus Status { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string OriginalSha256Hash { get; private set; } = null!;
    public Guid? FinalDocumentId { get; private set; }
    public Guid? FinalDocumentVersionId { get; private set; }
    public string? FinalSha256Hash { get; private set; }

    /// <summary>SHA-256 over a canonical summary of recipients+fields+their hashes+timestamps,
    /// computed once at completion — evidence that the envelope's own record (not just the PDF
    /// bytes) hasn't been tampered with. Computed in the Application layer (needs to serialize the
    /// full recipient/field graph) and assigned here via CompleteWithFinalDocument.</summary>
    public string? EnvelopeHash { get; private set; }

    /// <summary>The completion certificate as its OWN standalone PDF (not the pages appended to
    /// the signed document) — stored here, not in the Documents module, since it's a companion
    /// artifact of the envelope itself, not a version of the source document. See
    /// IPdfEnvelopeEmbeddingEngine.Embed's EmbedResult.CertificatePdfPath for where the bytes
    /// originate (a page-copy-only split off the same combined PDF, done to sidestep PdfSharp's
    /// font-cache crash on a second font-using PdfDocument instance — see that engine's doc
    /// comment).</summary>
    public byte[]? CertificateDocument { get; private set; }
    public string? CertificateHash { get; private set; }

    public IReadOnlyList<EnvelopeRecipient> Recipients => _recipients.AsReadOnly();
    public IReadOnlyList<EnvelopeField> Fields => _fields.AsReadOnly();
    public IReadOnlyList<DocumentSignature> DocumentSignatures => _documentSignatures.AsReadOnly();
    public IReadOnlyList<ConsentRecord> ConsentRecords => _consentRecords.AsReadOnly();

    private SignatureEnvelope() { } // EF Core

    public static SignatureEnvelope Create(
        Guid sourceDocumentId, Guid sourceDocumentVersionId, string title, string? message,
        Guid createdByUserId, SigningMode signingMode, DateTime? dueDateUtc, string originalSha256Hash, Guid organizationId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("El sobre debe tener un título.");
        }
        if (dueDateUtc is not null && dueDateUtc <= DateTime.UtcNow)
        {
            throw new DomainException("La fecha límite debe ser futura.");
        }

        return new SignatureEnvelope
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentVersionId = sourceDocumentVersionId,
            Title = title.Trim(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            SigningMode = signingMode,
            DueDateUtc = dueDateUtc,
            Status = EnvelopeStatus.Draft,
            OriginalSha256Hash = originalSha256Hash,
        };
    }

    public EnvelopeRecipient AddRecipient(string email, string fullName, int order)
    {
        EnsureDraft();
        var recipient = EnvelopeRecipient.Create(Id, email, fullName, order);
        _recipients.Add(recipient);
        return recipient;
    }

    public void RemoveRecipient(Guid recipientId)
    {
        EnsureDraft();
        var recipient = FindRecipient(recipientId);
        _fields.RemoveAll(f => f.RecipientId == recipientId);
        _recipients.Remove(recipient);
    }

    public EnvelopeField AddField(
        Guid recipientId, FieldType type, int pageNumber, double positionX, double positionY,
        double width, double height, bool required)
    {
        EnsureDraft();
        FindRecipient(recipientId); // throws if the recipient doesn't belong to this envelope
        var field = EnvelopeField.Create(Id, recipientId, type, pageNumber, positionX, positionY, width, height, required);
        _fields.Add(field);
        return field;
    }

    public void RemoveField(Guid fieldId)
    {
        EnsureDraft();
        _fields.Remove(FindField(fieldId));
    }

    public void UpdateField(Guid fieldId, double positionX, double positionY, double width, double height)
    {
        EnsureDraft();
        FindField(fieldId).UpdatePosition(positionX, positionY, width, height);
    }

    /// <summary>Resolves whether a recipient has an internal SDPP account — called by the
    /// Application handler once per recipient, right before Send(), after querying Identity.</summary>
    public void ResolveRecipientMatch(Guid recipientId, Guid? matchedUserId) => FindRecipient(recipientId).MarkMatched(matchedUserId);

    /// <summary>Dispatches the envelope: Draft -&gt; Sent -&gt; Pending, and marks the first wave of
    /// recipients Sent (everyone if Parallel, only the lowest Order if Sequential). The
    /// SignerAccessChallenge (token/OTP) for each newly-Sent recipient is created separately by the
    /// Application handler right after this call — token generation is a crypto/Infrastructure
    /// concern, not a domain one.</summary>
    public IReadOnlyList<EnvelopeRecipient> Send()
    {
        EnsureDraft();
        if (_recipients.Count == 0)
        {
            throw new DomainException("El sobre debe tener al menos un firmante antes de enviarse.");
        }
        if (_recipients.Any(r => !_fields.Any(f => f.RecipientId == r.Id)))
        {
            throw new DomainException("Todos los firmantes deben tener al menos un campo asignado antes de enviarse.");
        }

        var firstWave = SigningMode == SigningMode.Sequential
            ? [_recipients.OrderBy(r => r.Order).First()]
            : (IReadOnlyList<EnvelopeRecipient>)_recipients;

        foreach (var recipient in firstWave)
        {
            recipient.MarkSent();
        }

        Status = EnvelopeStatus.Pending;
        SentAtUtc = DateTime.UtcNow;
        return firstWave;
    }

    public void RegisterView(Guid recipientId, string? ipAddress)
    {
        EnsureNotTerminal();
        FindRecipient(recipientId).MarkViewed(ipAddress);
        if (Status is EnvelopeStatus.Sent or EnvelopeStatus.Pending)
        {
            Status = EnvelopeStatus.InProgress;
        }
    }

    /// <summary>Records acceptance of the electronic-signature consent declaration — required
    /// before RegisterSignature will allow this recipient to sign (fail-closed: no consent, no
    /// signature, enforced here in the domain, not just in the UI). Creates a first-class
    /// ConsentRecord (the authoritative evidentiary copy, later linked from DocumentSignature) while
    /// also still setting EnvelopeRecipient's own consent fields, so existing certificate/query code
    /// that reads those directly keeps working unchanged.</summary>
    public ConsentRecord RegisterConsent(Guid recipientId, string? ipAddress, string? userAgent, string authenticationMethod)
    {
        EnsureNotTerminal();
        var recipient = FindRecipient(recipientId);
        recipient.MarkConsentAccepted(ipAddress, userAgent);
        var consentRecord = ConsentRecord.Create(Id, recipientId, ipAddress, userAgent, authenticationMethod);
        _consentRecords.Add(consentRecord);
        return consentRecord;
    }

    /// <summary>Fills this recipient's fields and marks them Signed. Enforces sequential turn-taking
    /// and that every Required field belonging to this recipient ends up filled. Returns the next
    /// recipient to dispatch (Sequential mode only, null otherwise/if this was the last signer) so
    /// the Application handler knows who to notify next. Does NOT transition to Completed even if
    /// this was the last recipient — that only happens once the final PDF has actually been
    /// assembled, via CompleteWithFinalDocument.</summary>
    public EnvelopeRecipient? RegisterSignature(
        Guid recipientId, IReadOnlyList<(Guid FieldId, string? Value, byte[]? SignatureImage, SignatureMethodUsed? Method)> fieldValues,
        string? ipAddress, string? userAgent, string authMethod)
    {
        EnsureNotTerminal();
        var recipient = FindRecipient(recipientId);

        if (recipient.ConsentAcceptedAtUtc is null)
        {
            throw new DomainException("Debe aceptar el consentimiento de firma electrónica antes de firmar.");
        }

        if (SigningMode == SigningMode.Sequential)
        {
            var nextTurn = _recipients
                .Where(r => r.Status is not (RecipientStatus.Signed or RecipientStatus.Declined or RecipientStatus.Expired))
                .OrderBy(r => r.Order)
                .First();
            if (nextTurn.Id != recipientId)
            {
                throw new DomainException("Todavía no es el turno de este firmante.");
            }
        }

        foreach (var (fieldId, value, signatureImage, method) in fieldValues)
        {
            var field = FindField(fieldId);
            if (field.RecipientId != recipientId)
            {
                throw new DomainException("Este campo no pertenece a este firmante.");
            }
            field.Fill(value, signatureImage, method);
            field.AssignSignatureHash(ComputeFieldSignatureHash(recipient, field, ipAddress, userAgent));
        }

        var unfilledRequired = _fields.Any(f => f.RecipientId == recipientId && f.Required && !f.IsFilled);
        if (unfilledRequired)
        {
            throw new DomainException("Faltan campos obligatorios por completar.");
        }

        recipient.MarkSigned(ipAddress, authMethod);

        var remaining = _recipients.Where(r => r.Status is not (RecipientStatus.Signed or RecipientStatus.Declined or RecipientStatus.Expired)).ToList();
        if (remaining.Count == 0)
        {
            return null;
        }

        Status = EnvelopeStatus.PartiallySigned;

        if (SigningMode != SigningMode.Sequential)
        {
            return null;
        }

        var next = remaining.OrderBy(r => r.Order).First();
        next.MarkSent();
        return next;
    }

    /// <summary>Appends SDPP's platform attestation of this recipient's just-completed signature
    /// (see DocumentSignature's doc comment for what this cryptographic signature is and is not).
    /// The Application handler computes the canonical payload/hash and calls
    /// IKeyManagementService.SignAsync BEFORE calling this — signing itself is an Infrastructure
    /// concern this aggregate never performs, same separation as IPdfEnvelopeEmbeddingEngine.</summary>
    public DocumentSignature RecordCryptographicSignature(
        Guid recipientId, Guid documentId, Guid documentVersionId, string documentHashAtSigning,
        string canonicalPayloadHash, string cryptographicSignatureBase64, Guid publicKeyId, string algorithm,
        Guid? consentId, DateTime timestampUtc, string timestampSource)
    {
        FindRecipient(recipientId); // throws if the recipient doesn't belong to this envelope
        var documentSignature = DocumentSignature.Create(
            Id, recipientId, documentId, documentVersionId, documentHashAtSigning, canonicalPayloadHash,
            cryptographicSignatureBase64, publicKeyId, algorithm, consentId, timestampUtc, timestampSource);
        _documentSignatures.Add(documentSignature);
        return documentSignature;
    }

    public void RegisterDecline(Guid recipientId, string reason)
    {
        EnsureNotTerminal();
        FindRecipient(recipientId).MarkDeclined(reason);
        Status = EnvelopeStatus.Declined;
    }

    public void Cancel()
    {
        EnsureNotTerminal();
        Status = EnvelopeStatus.Cancelled;
    }

    public void Expire()
    {
        EnsureNotTerminal();
        Status = EnvelopeStatus.Expired;
    }

    /// <summary>Recipients whose turn is actionable (Sent/Viewed) and haven't been reminded
    /// recently enough — the (Fase I) lifecycle job's own eligibility rule, kept here so "who's due
    /// for a reminder" is a domain fact, not logic scattered in Infrastructure.</summary>
    public IReadOnlyList<EnvelopeRecipient> GetRecipientsDueForReminder(DateTime asOfUtc, TimeSpan interval, int maxReminders) =>
        _recipients.Where(r =>
            r.Status is RecipientStatus.Sent or RecipientStatus.Viewed &&
            r.ReminderCount < maxReminders &&
            (r.LastReminderSentAtUtc ?? r.SentAtUtc) is { } lastContact &&
            lastContact <= asOfUtc - interval)
            .ToList();

    public void MarkReminderSent(Guid recipientId) => FindRecipient(recipientId).MarkReminderSent();

    /// <summary>Called once, after every recipient has signed AND the Application handler has
    /// assembled/uploaded/locked the final combined PDF.</summary>
    public void CompleteWithFinalDocument(
        Guid finalDocumentId, Guid finalDocumentVersionId, string finalSha256Hash, byte[]? certificateDocument, string? certificateHash)
    {
        EnsureNotTerminal();
        if (_recipients.Any(r => r.Status != RecipientStatus.Signed))
        {
            throw new DomainException("No todos los firmantes han firmado todavía.");
        }

        FinalDocumentId = finalDocumentId;
        FinalDocumentVersionId = finalDocumentVersionId;
        FinalSha256Hash = finalSha256Hash;
        CertificateDocument = certificateDocument;
        CertificateHash = certificateHash;
        CompletedAtUtc = DateTime.UtcNow;
        Status = EnvelopeStatus.Completed;
        EnvelopeHash = ComputeEnvelopeHash();
    }

    /// <summary>Computes the same hash CompleteWithFinalDocument will assign to EnvelopeHash,
    /// without mutating state — lets the Application layer print it on the certificate page, which
    /// must be drawn before the final PDF (and thus before CompleteWithFinalDocument) exists. Safe
    /// to call once every recipient has signed, since recipient/field state won't change again
    /// before completion.</summary>
    public string PreviewEnvelopeHash() => ComputeEnvelopeHash();

    private void EnsureDraft()
    {
        if (Status != EnvelopeStatus.Draft)
        {
            throw new DomainException($"El sobre ya fue enviado y no admite cambios (estado actual: {Status}).");
        }
    }

    private void EnsureNotTerminal()
    {
        if (Status is EnvelopeStatus.Completed or EnvelopeStatus.Declined or EnvelopeStatus.Cancelled or EnvelopeStatus.Expired)
        {
            throw new DomainException($"El sobre ya está en un estado final ('{Status}') y no admite esta acción.");
        }
    }

    /// <summary>Canonical, order-fixed representation of everything that makes this exact
    /// signature what it is — envelope/document identity, signer identity, the field's own
    /// definitive coordinates (the fraction-based "PDF_FRACTION" system this module uses
    /// throughout, per EnvelopeField's own doc comment), the actual data signed, and the
    /// evidentiary context (consent/IP/UA/timestamp). Any change to any of these invalidates the
    /// hash — that's the point.</summary>
    private string ComputeFieldSignatureHash(EnvelopeRecipient recipient, EnvelopeField field, string? ipAddress, string? userAgent)
    {
        var dataDigest = field.SignatureImage is { Length: > 0 } image
            ? Convert.ToHexStringLower(SHA256.HashData(image))
            : field.Value ?? string.Empty;

        var canonical = string.Join('|',
            Id, SourceDocumentId, SourceDocumentVersionId, recipient.Id, recipient.Email,
            field.Id, field.Type, field.SignatureMethod?.ToString() ?? string.Empty, dataDigest,
            field.PageNumber, field.PositionX, field.PositionY, field.Width, field.Height, "PDF_FRACTION",
            field.FilledAtUtc?.ToString("O") ?? string.Empty, recipient.ConsentAcceptedAtUtc?.ToString("O") ?? string.Empty,
            ipAddress ?? string.Empty, userAgent ?? string.Empty);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>SHA-256 over every recipient/field's own state (status, timestamps, IPs, per-field
    /// SignatureHash) — evidence that the envelope's own record wasn't altered after completion,
    /// independent of whether the final PDF bytes themselves are intact (that's FinalSha256Hash's
    /// job). Computed once, at completion, from the fully-signed state.</summary>
    private string ComputeEnvelopeHash()
    {
        var recipientsPart = string.Join(';', _recipients.OrderBy(r => r.Order).Select(r => string.Join('|',
            r.Id, r.Email, r.Status, r.SentAtUtc?.ToString("O"), r.ViewedAtUtc?.ToString("O"), r.ViewedIpAddress,
            r.SignedAtUtc?.ToString("O"), r.SignedIpAddress, r.AuthMethodUsed, r.ConsentAcceptedAtUtc?.ToString("O"))));
        var fieldsPart = string.Join(';', _fields.OrderBy(f => f.Id).Select(f => string.Join('|', f.Id, f.SignatureHash)));

        var canonical = string.Join('~', Id, SourceDocumentId, OriginalSha256Hash, recipientsPart, fieldsPart);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private EnvelopeRecipient FindRecipient(Guid recipientId) =>
        _recipients.FirstOrDefault(r => r.Id == recipientId)
            ?? throw new DomainException($"El firmante '{recipientId}' no pertenece a este sobre.");

    private EnvelopeField FindField(Guid fieldId) =>
        _fields.FirstOrDefault(f => f.Id == fieldId)
            ?? throw new DomainException($"El campo '{fieldId}' no pertenece a este sobre.");
}
