using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.EnvelopeLifecycle;

/// <summary>
/// The Hangfire recurring job (registered hourly in Program.cs as "signature-envelope-lifecycle")
/// that makes SignatureEnvelope.Expire() and the reminder cadence actually run — previously
/// GetPastDueAsync/Expire()/SignatureEnvelopeExpiredV1 all existed but nothing ever called them.
/// Two independent passes, each with its own SaveChanges: expiring past-due envelopes must never be
/// blocked by (or roll back alongside) a failure while sending a reminder for a different envelope.
/// </summary>
public sealed class EnvelopeLifecycleJob(
    ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork, IIntegrationEventPublisher integrationEventPublisher,
    IEmailSender emailSender, INotificationRepository notificationRepository, IPublicWebLinkBuilder publicWebLinkBuilder)
{
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromDays(3);
    private const int MaxReminders = 3;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await ExpirePastDueAsync(cancellationToken);
        await SendRemindersAsync(cancellationToken);
    }

    private async Task ExpirePastDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var pastDue = await repository.GetPastDueAsync(now, cancellationToken);

        foreach (var envelope in pastDue)
        {
            try
            {
                envelope.Expire();
            }
            catch (SDPP.BuildingBlocks.Domain.DomainException)
            {
                continue; // already terminal by the time we got here — nothing to do
            }

            notificationRepository.Add(Domain.Aggregates.InAppNotification.Create(
                envelope.CreatedByUserId, NotificationType.EnvelopeExpired,
                "Sobre vencido", $"El sobre \"{envelope.Title}\" venció sin completarse.", envelope.Id));

            await integrationEventPublisher.PublishAsync(
                new SignatureEnvelopeExpiredV1(Guid.NewGuid(), now, envelope.Id, envelope.SourceDocumentId), cancellationToken);
        }

        if (pastDue.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var active = await repository.GetActiveAsync(cancellationToken);
        var anyReminderSent = false;

        foreach (var envelope in active)
        {
            var due = envelope.GetRecipientsDueForReminder(now, ReminderInterval, MaxReminders);
            foreach (var recipient in due)
            {
                envelope.MarkReminderSent(recipient.Id);
                anyReminderSent = true;

                var link = publicWebLinkBuilder.BuildVerificationUrl(envelope.Id);
                await emailSender.SendAsync(
                    recipient.Email, $"Recordatorio: firma pendiente — {envelope.Title}",
                    $"Tienes un documento pendiente de firma: \"{envelope.Title}\". Verifica el estado en {link}.",
                    cancellationToken: cancellationToken);

                if (recipient.MatchedUserId is { } matchedUserId)
                {
                    notificationRepository.Add(Domain.Aggregates.InAppNotification.Create(
                        matchedUserId, NotificationType.ReminderSent,
                        "Recordatorio de firma pendiente", $"Tienes pendiente firmar \"{envelope.Title}\".", envelope.Id));
                }
            }
        }

        if (anyReminderSent)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
