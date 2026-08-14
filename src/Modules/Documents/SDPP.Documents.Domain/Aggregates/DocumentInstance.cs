using SDPP.BuildingBlocks.Domain;
using SDPP.Documents.Domain.Enums;
using SDPP.Documents.Domain.Events;
using SDPP.Documents.Domain.ValueObjects;

namespace SDPP.Documents.Domain.Aggregates;

/// <summary>
/// One physical file (a specific format, a specific set of bytes) belonging to a
/// <see cref="DocumentVersion"/> — was the sole aggregate before the identity/fingerprint model
/// (see docs/02-domain/domain-model.md §2.2 and the integrity proposal); renamed from
/// <c>Document</c> to make explicit that this is the "physical instance" tier, not the document's
/// permanent identity (that's <see cref="LogicalDocument"/>). Owns its ConversionJob entities and
/// enforces every invariant listed there — most importantly that a document blocked by
/// inspection cannot be queued for conversion. Single responsibility: manage documents and their
/// conversion jobs — no classification, hashing, watermarking, or business-justification form (see
/// the Panel de Conversión simplification; those concerns now live entirely in Classification.Api).
/// </summary>
public sealed class DocumentInstance : AggregateRoot<Guid>
{
    private readonly List<ConversionJob> _jobs = [];

    public Guid OwnerId { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public StoragePath StorageLocation { get; private set; } = null!;
    public int? PageCount { get; private set; }
    public DocumentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }

    /// <summary>The version of the document's content this instance represents — several
    /// instances (different formats, or a re-save with only metadata changed) can share the same
    /// DocumentVersionId; that's what lets a format conversion skip reclassification. Hash,
    /// classification, risk/category/labels and integrity/protection data for this instance now
    /// live in the Classification module (DocumentIntegrityRecord/DocumentVersionFingerprint,
    /// keyed by this document's/version's id) — see the "Clasificación de Activos de Información"
    /// extraction; Documents.Api enriches its own read responses by calling Classification.Api
    /// rather than storing a local copy.</summary>
    public Guid DocumentVersionId { get; private set; }

    /// <summary>Set when this instance was produced by converting another instance within SDPP
    /// (worker-driven conversions always know this for certain) — explicit lineage, replacing the
    /// old one-directional-only signal via ConversionJob.OutputDocumentId.</summary>
    public Guid? ConvertedFromInstanceId { get; private set; }

    public IReadOnlyList<ConversionJob> Jobs => _jobs.AsReadOnly();

    private DocumentInstance() { } // EF Core

    public static DocumentInstance Upload(
        Guid ownerId, string originalFileName, string contentType, long sizeBytes,
        Guid documentVersionId, Guid? convertedFromInstanceId = null)
    {
        if (sizeBytes <= 0)
        {
            throw new DomainException("El tamaño del archivo debe ser mayor a cero.");
        }

        var document = new DocumentInstance
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Status = DocumentStatus.Uploaded,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = ownerId,
            DocumentVersionId = documentVersionId,
            ConvertedFromInstanceId = convertedFromInstanceId,
        };
        document.StorageLocation = StoragePath.ForDocument(document.Id, originalFileName);

        document.Raise(DocumentUploaded.Create(document.Id, ownerId, originalFileName, contentType, sizeBytes));
        return document;
    }

    public void BeginInspection()
    {
        if (Status != DocumentStatus.Uploaded)
        {
            throw new DomainException($"Solo un documento en estado Uploaded puede iniciar inspección (estado actual: {Status}).");
        }
        Status = DocumentStatus.Inspecting;
    }

    /// <summary>
    /// Applies the outcome of automatic/hybrid inspection now run by Classification.Api — this
    /// aggregate no longer stores the classification itself (see the "Clasificación de Activos de
    /// Información" extraction), only whether it can leave the Inspecting state. A document can
    /// never be marked Ready with a pending manual review — see docs/02-domain/domain-model.md
    /// §2.2, invariant 1.
    /// </summary>
    public void CompleteInspection(bool requiresManualReview) =>
        Status = requiresManualReview ? DocumentStatus.Inspecting : DocumentStatus.Ready;

    public void Block(string reason)
    {
        Status = DocumentStatus.Blocked;
        Raise(DocumentBlocked.Create(Id, reason));
    }

    /// <summary>Marks this instance permanently read-only — used only on the final artifact of a
    /// completed signature envelope (Signature.Api calls this via the internal /lock endpoint right
    /// after producing it), never on a source document. Irreversible: there is no Unlock.</summary>
    public void Lock()
    {
        if (Status != DocumentStatus.Ready)
        {
            throw new DomainException($"Solo un documento en estado Ready puede bloquearse (estado actual: {Status}).");
        }
        Status = DocumentStatus.Locked;
    }

    /// <summary>
    /// Creates a new ConversionJob for this document — no mandatory form, no policy gate. See
    /// RequestConversionHandler, which queues the job right after creating it.
    /// </summary>
    public ConversionJob RequestConversion(OperationType operationType)
    {
        if (Status is DocumentStatus.Blocked or DocumentStatus.Deleted or DocumentStatus.PendingDeletion or DocumentStatus.Locked)
        {
            throw new DomainException($"No se puede convertir un documento en estado '{Status}'.");
        }

        var job = ConversionJob.Create(Id, operationType);
        _jobs.Add(job);
        Raise(Events.ConversionRequested.Create(job.Id, Id, operationType));
        return job;
    }

    public void QueueJob(Guid jobId) => FindJob(jobId).MarkQueued();

    public void StartProcessingJob(Guid jobId) => FindJob(jobId).MarkProcessing();

    public void CompleteJob(Guid jobId, Guid outputDocumentId, string engineUsed, int durationMs)
    {
        var job = FindJob(jobId);
        job.MarkCompleted(outputDocumentId, engineUsed, durationMs);
        Raise(Events.ConversionCompleted.Create(job.Id, Id, outputDocumentId, engineUsed, durationMs));
    }

    public void FailJob(Guid jobId, string errorDetail)
    {
        var job = FindJob(jobId);
        job.MarkFailed(errorDetail);
        Raise(Events.ConversionFailed.Create(job.Id, Id, errorDetail));
    }

    private ConversionJob FindJob(Guid jobId) =>
        _jobs.FirstOrDefault(j => j.Id == jobId)
            ?? throw new DomainException($"El job '{jobId}' no pertenece a este documento.");
}
