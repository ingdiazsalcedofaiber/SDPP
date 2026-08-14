namespace SDPP.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Append-only record of a domain event captured in the same transaction as the aggregate that
/// raised it. A background processor (<see cref="OutboxProcessor"/>) publishes it to RabbitMQ
/// afterwards, guaranteeing at-least-once delivery without requiring a distributed transaction
/// between SQL Server and the message broker.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public required string Type { get; init; }
    public required string Content { get; init; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
}
