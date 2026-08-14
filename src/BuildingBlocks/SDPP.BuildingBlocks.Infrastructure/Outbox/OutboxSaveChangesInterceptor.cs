using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SDPP.BuildingBlocks.Domain;

namespace SDPP.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Before every SaveChanges, drains the domain events raised by tracked aggregates and writes
/// them as <see cref="OutboxMessage"/> rows in the very same transaction — this is what
/// guarantees the event is never lost even if the process crashes right after commit.
/// </summary>
public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AddOutboxMessages(DbContext? context)
    {
        if (context is null) return;

        var aggregatesWithEvents = context.ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var outboxMessages = aggregatesWithEvents
            .SelectMany(aggregate => aggregate.DomainEvents)
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = domainEvent.OccurredAtUtc,
                Type = domainEvent.GetType().AssemblyQualifiedName!,
                Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            })
            .ToList();

        context.Set<OutboxMessage>().AddRange(outboxMessages);

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }
    }
}
