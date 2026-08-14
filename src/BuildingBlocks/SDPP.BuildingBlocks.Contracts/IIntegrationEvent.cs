namespace SDPP.BuildingBlocks.Contracts;

/// <summary>
/// Marker for versioned events exchanged between bounded contexts over RabbitMQ (the "published
/// language" of the context map, see docs/02-domain/domain-model.md §1). Each event type name is
/// suffixed with a version (V1, V2...) so contracts can evolve without breaking consumers.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}
