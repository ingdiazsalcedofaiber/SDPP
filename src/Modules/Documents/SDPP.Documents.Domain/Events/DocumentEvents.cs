using SDPP.BuildingBlocks.Domain;

namespace SDPP.Documents.Domain.Events;

public sealed record DocumentUploaded(
    Guid EventId, DateTime OccurredAtUtc, Guid DocumentId, Guid OwnerId,
    string OriginalFileName, string ContentType, long SizeBytes) : IDomainEvent
{
    public static DocumentUploaded Create(Guid documentId, Guid ownerId, string fileName, string contentType, long size) =>
        new(Guid.NewGuid(), DateTime.UtcNow, documentId, ownerId, fileName, contentType, size);
}

public sealed record DocumentBlocked(
    Guid EventId, DateTime OccurredAtUtc, Guid DocumentId, string Reason) : IDomainEvent
{
    public static DocumentBlocked Create(Guid documentId, string reason) =>
        new(Guid.NewGuid(), DateTime.UtcNow, documentId, reason);
}
