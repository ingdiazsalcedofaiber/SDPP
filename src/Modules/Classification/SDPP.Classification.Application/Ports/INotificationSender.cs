namespace SDPP.Classification.Application.Ports;

public sealed record NotificationMessage(
    Guid DocumentId,
    Guid AuditId,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// Extension point for the "notificar al administrador" requirement on Altamente Sensible
/// documents. The default implementation (AuditLoggingNotificationSender, in
/// SDPP.Classification.Infrastructure/Protection) publishes AdminNotificationRequestedV1 so the
/// event is recorded in the audit trail; wiring a real email/Teams/Slack sender later means adding
/// a second implementation here, not touching ProtectionEngine.
/// </summary>
public interface INotificationSender
{
    Task NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
