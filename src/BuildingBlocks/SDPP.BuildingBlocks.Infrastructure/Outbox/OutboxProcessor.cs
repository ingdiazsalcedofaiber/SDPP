using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SDPP.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Recurring background job (scheduled by Hangfire in each module's Api project, see
/// docs/01-architecture/c4-diagrams.md) that publishes pending outbox messages to RabbitMQ via
/// MassTransit and marks them processed. Runs at least every few seconds so the delay between a
/// domain event and its integration-event counterpart stays small without coupling the write
/// path to the broker being reachable.
/// </summary>
public sealed class OutboxProcessor<TContext>(
    TContext dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<OutboxProcessor<TContext>> logger)
    where TContext : DbContext
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedAtUtc == null && m.AttemptCount < MaxAttempts)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                var eventType = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"No se pudo resolver el tipo '{message.Type}'.");
                var domainEvent = JsonSerializer.Deserialize(message.Content, eventType)
                    ?? throw new InvalidOperationException("El payload del outbox no se pudo deserializar.");

                await publishEndpoint.Publish(domainEvent, eventType, cancellationToken);

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                message.Error = ex.Message;
                logger.LogError(
                    ex, "Fallo al publicar mensaje de outbox {MessageId} (intento {Attempt}/{MaxAttempts})",
                    message.Id, message.AttemptCount, MaxAttempts);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
