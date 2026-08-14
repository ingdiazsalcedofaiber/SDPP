namespace SDPP.BuildingBlocks.Contracts.Documents;

/// <summary>
/// Published once a ConversionJob has been approved by the policy engine (Allow, or an
/// AwaitingApproval request that was subsequently approved) and is ready for a worker to pick
/// up. Consumed by SDPP.Conversion.Worker.
/// </summary>
public sealed record ConversionRequestedV1(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid JobId,
    Guid DocumentId,
    string OperationType,
    string StorageBucket,
    string StorageObjectKey,
    string DeclaredClassification,
    IReadOnlyDictionary<string, string> OperationParameters,
    ActorSnapshot Actor,
    int RiskScore = 0,
    string? Category = null,
    IReadOnlyList<string>? Labels = null,
    string? Area = null) : IIntegrationEvent;
