namespace SDPP.Signature.Domain.Enums;

/// <summary>
/// Literal transition table (see SignatureEnvelope for the guards that enforce it):
/// <c>Draft -&gt; Send() -&gt; Sent -&gt; (first wave dispatched, immediate) -&gt; Pending -&gt;
/// (a recipient views or signs, first activity) -&gt; InProgress -&gt; (at least one signature
/// captured, not all) -&gt; PartiallySigned -&gt; (last recipient signs + final PDF assembled) -&gt;
/// Completed</c>. From any non-terminal state: a decline -&gt; <see cref="Declined"/>, the creator
/// cancelling -&gt; <see cref="Cancelled"/>, the Hangfire lifecycle job crossing the due date -&gt;
/// <see cref="Expired"/>.
/// </summary>
public enum EnvelopeStatus
{
    Draft,
    Sent,
    Pending,
    InProgress,
    PartiallySigned,
    Completed,
    Declined,
    Cancelled,
    Expired,
}

public enum SigningMode
{
    Sequential,
    Parallel,
}

public enum RecipientStatus
{
    Pending,
    Sent,
    Viewed,
    Signed,
    Declined,
    Expired,
}

public enum FieldType
{
    Signature,
    Initials,
    Date,
    Name,
    Title,
    Text,
    Stamp,
    Checkbox,

    /// <summary>The official "APROBADO GERENCIA LEGAL" stamp — deliberately NOT image-bearing like
    /// Stamp: the recipient never draws or uploads anything for this field, since accepting
    /// arbitrary client-supplied bytes for an official approval mark would make it forgeable. SDPP
    /// generates the graphic itself at document-assembly time (see
    /// PdfSharpEnvelopeEmbeddingEngine.DrawLegalApprovalStamp) — filling this field is only ever an
    /// explicit confirmation click. Restricted, fail-closed, to a single configured recipient email
    /// (CompleteRecipientSigningCommand's LegalApprovalStampEmail check) — never enforced only in
    /// the UI.</summary>
    LegalApprovalStamp,
}

/// <summary>How a Signature/Initials/Stamp field's image was produced — carried into the audit
/// trail (EnvelopeRecipientSignedV1). Deliberately open to a future <c>DigitalCertificate</c> value
/// once real PKI integration exists (see OperationType.DigitalSign's own comment on why that isn't
/// implemented yet) — no other code needs to change to add it, this enum is purely descriptive.</summary>
public enum SignatureMethodUsed
{
    Drawn,
    Typed,
    Uploaded,
    Reused,
}

/// <summary>Lifecycle of an SDPP platform signing key (see SignatureKey/IKeyManagementService).
/// Revoking a key never invalidates DocumentSignature rows already made with it — only stops it
/// from being handed out for new ones.</summary>
public enum SignatureKeyStatus
{
    Active,
    Revoked,
}

public enum NotificationType
{
    EnvelopeSent,
    EnvelopeViewed,
    EnvelopeSigned,
    EnvelopeCompleted,
    EnvelopeDeclined,
    EnvelopeCancelled,
    EnvelopeExpired,
    ReminderSent,
}
