using SDPP.BuildingBlocks.Domain;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Domain.Aggregates;

/// <summary>
/// A single transformation requested over a document. Modeled as a child entity of
/// <see cref="DocumentInstance"/> (not its own aggregate root) because its lifecycle is entirely
/// owned by the document it operates on — see docs/02-domain/domain-model.md §2.3.
/// </summary>
public sealed class ConversionJob : Entity<Guid>
{
    public Guid DocumentId { get; private set; }
    public OperationType OperationType { get; private set; }
    public ConversionJobStatus Status { get; private set; }
    public string? EngineUsed { get; private set; }
    public int? DurationMs { get; private set; }
    public Guid? OutputDocumentId { get; private set; }
    public string? ErrorDetail { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private ConversionJob() { } // EF Core

    internal static ConversionJob Create(Guid documentId, OperationType operationType) => new()
    {
        Id = Guid.NewGuid(),
        DocumentId = documentId,
        OperationType = operationType,
        Status = ConversionJobStatus.PendingForm,
        CreatedAtUtc = DateTime.UtcNow,
    };

    internal void MarkQueued()
    {
        EnsureStatus(ConversionJobStatus.PendingForm);
        Status = ConversionJobStatus.Queued;
    }

    internal void MarkProcessing()
    {
        EnsureStatus(ConversionJobStatus.Queued);
        Status = ConversionJobStatus.Processing;
    }

    internal void MarkCompleted(Guid outputDocumentId, string engineUsed, int durationMs)
    {
        EnsureStatus(ConversionJobStatus.Processing);
        OutputDocumentId = outputDocumentId;
        EngineUsed = engineUsed;
        DurationMs = durationMs;
        Status = ConversionJobStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    internal void MarkFailed(string errorDetail)
    {
        ErrorDetail = errorDetail;
        Status = ConversionJobStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    private void EnsureStatus(params ConversionJobStatus[] expected)
    {
        if (!expected.Contains(Status))
        {
            throw new DomainException(
                $"No se puede realizar esta transición desde el estado '{Status}'. Estados válidos: {string.Join(", ", expected)}.");
        }
    }
}
