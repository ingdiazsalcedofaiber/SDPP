using MediatR;
using SDPP.Audit.Application.Ports;
using SDPP.BuildingBlocks.Application;

namespace SDPP.Audit.Application.UseCases.ExportRecords;

public sealed record AuditRecordExportDto(
    long Id, DateTime OccurredAtUtc, string EventType, string ActorFullName, string ActorEmail, string? ActorIp,
    Guid? SubjectDocumentId, string PayloadJson, string PreviousRecordHash, string RecordHash);

/// <summary>Backs the internal-only GET /api/v1/audit/records/export, consumed by Signature.Api's
/// Evidence Package export (see IAuditClient.GetRecordsAsync) to assemble a real audit-trail.json —
/// unlike VerifyIntegrityQuery (which returns only a pass/fail summary for the public verifier),
/// this returns the full records, since an Evidence Package is a private, authenticated artifact
/// (gated by the SAME ownership check as the certificate download) meant to contain complete
/// evidentiary detail.</summary>
public sealed record GetRecordsBySubjectsQuery(IReadOnlyList<Guid> SubjectIds) : IQuery<IReadOnlyList<AuditRecordExportDto>>;

public sealed class GetRecordsBySubjectsHandler(IAuditRecordRepository repository)
    : IRequestHandler<GetRecordsBySubjectsQuery, Result<IReadOnlyList<AuditRecordExportDto>>>
{
    public async Task<Result<IReadOnlyList<AuditRecordExportDto>>> Handle(GetRecordsBySubjectsQuery request, CancellationToken cancellationToken)
    {
        var records = await repository.GetBySubjectIdsAsync(request.SubjectIds, cancellationToken);

        var dtos = records
            .Select(r => new AuditRecordExportDto(
                r.Id, r.OccurredAtUtc, r.EventType, r.ActorFullName, r.ActorEmail, r.ActorIp,
                r.SubjectDocumentId, r.PayloadJson, r.PreviousRecordHash, r.RecordHash))
            .ToList();

        return Result.Success<IReadOnlyList<AuditRecordExportDto>>(dtos);
    }
}
