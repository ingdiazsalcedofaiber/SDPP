using SDPP.Audit.Domain.Aggregates;
using SDPP.BuildingBlocks.Application;

namespace SDPP.Audit.Application.UseCases.RecordEvent;

/// <summary>
/// One command per consumed integration event (DocumentUploadedV1, ConversionCompletedV1, etc. —
/// see docs/05-security/audit-and-traceability.md §1). Every field required by the traceability
/// requirement is already present on ActorContext + PayloadJson; this handler's only job is to
/// extend the hash chain correctly under concurrent writers.
/// </summary>
public sealed record RecordEventCommand(
    string EventType, DateTime OccurredAtUtc, ActorContext Actor, Guid? SubjectDocumentId, string PayloadJson)
    : ICommand<long>;
