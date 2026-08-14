using MediatR;
using SDPP.Audit.Application.Ports;
using SDPP.Audit.Domain.Aggregates;
using SDPP.BuildingBlocks.Application;

namespace SDPP.Audit.Application.UseCases.RecordEvent;

public sealed class RecordEventHandler(IAuditRecordRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<RecordEventCommand, Result<long>>
{
    public async Task<Result<long>> Handle(RecordEventCommand request, CancellationToken cancellationToken)
    {
        var previousHash = await repository.GetLastRecordHashAsync(cancellationToken);

        var record = AuditRecord.Create(
            previousHash, request.EventType, request.OccurredAtUtc, request.Actor, request.SubjectDocumentId, request.PayloadJson);

        repository.Add(record);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(record.Id);
    }
}
