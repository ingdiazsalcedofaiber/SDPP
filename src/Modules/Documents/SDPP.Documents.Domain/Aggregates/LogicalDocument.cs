using SDPP.BuildingBlocks.Domain;

namespace SDPP.Documents.Domain.Aggregates;

/// <summary>
/// The permanent identity of a document — a UUID that never changes no matter how many formats,
/// conversions, or real content edits a document goes through (see the integrity-and-fingerprint
/// proposal, "Identidad, fingerprint y hash físico"). Everything that changes over time
/// (classification, content, physical bytes) lives one or two levels below, on
/// <see cref="DocumentVersion"/>/<see cref="DocumentInstance"/> — this aggregate only tracks which
/// version is current.
/// </summary>
public sealed class LogicalDocument : AggregateRoot<Guid>
{
    public Guid OwnerId { get; private set; }
    public Guid CurrentVersionId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LogicalDocument() { } // EF Core

    /// <summary>Created before its initial DocumentVersion exists (that version's factory needs
    /// this aggregate's Id) — callers must follow up with <see cref="AdvanceCurrentVersion"/>
    /// once the version is created, before persisting either. Both are always saved together in
    /// the same transaction, so this brief in-memory inconsistency never reaches the database.</summary>
    public static LogicalDocument Create(Guid ownerId)
    {
        return new LogicalDocument
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>Advances "the current version" pointer — called only when a genuinely new
    /// DocumentVersion is created (Initial/PartialModification/TotalModification), never for a
    /// format-only or metadata-only instance, which attaches to the existing current version.</summary>
    public void AdvanceCurrentVersion(Guid newVersionId) => CurrentVersionId = newVersionId;
}
