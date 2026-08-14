using SDPP.BuildingBlocks.Domain;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Domain.Events;

public sealed record ConversionRequested(
    Guid EventId, DateTime OccurredAtUtc, Guid JobId, Guid DocumentId, OperationType OperationType) : IDomainEvent
{
    public static ConversionRequested Create(Guid jobId, Guid documentId, OperationType operationType) =>
        new(Guid.NewGuid(), DateTime.UtcNow, jobId, documentId, operationType);
}

public sealed record ConversionCompleted(
    Guid EventId, DateTime OccurredAtUtc, Guid JobId, Guid DocumentId, Guid OutputDocumentId,
    string EngineUsed, int DurationMs) : IDomainEvent
{
    public static ConversionCompleted Create(Guid jobId, Guid documentId, Guid outputDocumentId, string engineUsed, int durationMs) =>
        new(Guid.NewGuid(), DateTime.UtcNow, jobId, documentId, outputDocumentId, engineUsed, durationMs);
}

public sealed record ConversionFailed(
    Guid EventId, DateTime OccurredAtUtc, Guid JobId, Guid DocumentId, string ErrorDetail) : IDomainEvent
{
    public static ConversionFailed Create(Guid jobId, Guid documentId, string errorDetail) =>
        new(Guid.NewGuid(), DateTime.UtcNow, jobId, documentId, errorDetail);
}
