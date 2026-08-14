using MassTransit;
using SDPP.BuildingBlocks.Application;

namespace SDPP.BuildingBlocks.Infrastructure.Messaging;

public sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class =>
        publishEndpoint.Publish(integrationEvent, cancellationToken);
}
