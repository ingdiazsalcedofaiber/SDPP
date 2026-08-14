namespace SDPP.BuildingBlocks.Domain;

/// <summary>Marker for events raised by an aggregate as a result of a state change.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}
