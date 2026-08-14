using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Infrastructure.Protection;

/// <summary>
/// Default INotificationSender: no email/Teams/Slack channel is configured yet, so "notify the
/// admin" means publishing AdminNotificationRequestedV1, which Audit records like every other
/// integration event — at minimum the event is never silently dropped. Swapping in a real channel
/// later means adding a second INotificationSender implementation and changing one DI
/// registration, not touching ProtectionEngine.
/// </summary>
public sealed class AuditLoggingNotificationSender(IIntegrationEventPublisher integrationEventPublisher) : INotificationSender
{
    public Task NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default) =>
        integrationEventPublisher.PublishAsync(
            new AdminNotificationRequestedV1(
                Guid.NewGuid(), DateTime.UtcNow, message.DocumentId, message.AuditId, message.Subject, message.Body),
            cancellationToken);
}
