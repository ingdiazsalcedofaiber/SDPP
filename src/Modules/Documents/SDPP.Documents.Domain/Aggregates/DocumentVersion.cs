using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Domain.Aggregates;

/// <summary>
/// One real state of a document's content — created only when the content actually changes
/// (Initial upload, PartialModification, or TotalModification; see IChangeDetectionService).
/// Multiple DocumentInstance rows (different formats, re-saves with only metadata changed) can
/// point at the same DocumentVersion — that's the whole point: converting a version to another
/// format never creates a new version, so its classification is never recomputed. Never deleted,
/// chained via <see cref="PreviousVersionId"/> so the full history survives forever, same
/// "nunca eliminar" guarantee the Audit module already enforces for AuditRecord.
///
/// The fingerprint and classification for this version now live in the Classification module
/// (DocumentVersionFingerprint, keyed by this version's id) — see the "Clasificación de Activos
/// de Información" extraction. This aggregate keeps only the document-lifecycle/versioning
/// identity: which logical document this is a version of, its position in the chain, and what
/// kind of change produced it.
/// </summary>
public sealed class DocumentVersion : SDPP.BuildingBlocks.Domain.AggregateRoot<Guid>
{
    public Guid LogicalDocumentId { get; private set; }
    public int VersionNumber { get; private set; }

    public ChangeType ChangeTypeFromPrevious { get; private set; }
    public Guid? PreviousVersionId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private DocumentVersion() { } // EF Core

    public static DocumentVersion CreateInitial(Guid logicalDocumentId, Guid createdByUserId) => new()
    {
        Id = Guid.NewGuid(),
        LogicalDocumentId = logicalDocumentId,
        VersionNumber = 1,
        ChangeTypeFromPrevious = ChangeType.Initial,
        CreatedByUserId = createdByUserId,
        CreatedAtUtc = DateTime.UtcNow,
    };

    public static DocumentVersion CreateNext(DocumentVersion previous, ChangeType changeType, Guid createdByUserId)
    {
        if (changeType is not (ChangeType.PartialModification or ChangeType.TotalModification))
        {
            throw new SDPP.BuildingBlocks.Domain.DomainException(
                $"Solo una modificación parcial o total crea una nueva versión (recibido: '{changeType}').");
        }

        return new DocumentVersion
        {
            Id = Guid.NewGuid(),
            LogicalDocumentId = previous.LogicalDocumentId,
            VersionNumber = previous.VersionNumber + 1,
            ChangeTypeFromPrevious = changeType,
            PreviousVersionId = previous.Id,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }
}
