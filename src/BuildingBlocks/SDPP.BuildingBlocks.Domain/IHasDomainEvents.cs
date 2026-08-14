namespace SDPP.BuildingBlocks.Domain;

/// <summary>Non-generic surface of AggregateRoot&lt;TId&gt; so infrastructure code (e.g. the outbox
/// interceptor) can discover raised events regardless of the aggregate's Id type.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
