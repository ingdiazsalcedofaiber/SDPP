namespace SDPP.BuildingBlocks.Application;

/// <summary>
/// Port used by application handlers to publish an already-translated integration event (a
/// SDPP.BuildingBlocks.Contracts type — the bounded context's "published language", see
/// docs/02-domain/domain-model.md §1) after the use case's own transaction has committed.
/// Handlers that need to enrich the event with request-scoped context unavailable to the domain
/// layer (actor IP/hostname, storage location, etc. — see
/// docs/05-security/audit-and-traceability.md §1.1) build the event themselves and call this
/// directly, rather than relying purely on the generic outbox/domain-event passthrough (which
/// still runs for in-process/same-context concerns, see SDPP.BuildingBlocks.Infrastructure.Outbox).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class;
}
