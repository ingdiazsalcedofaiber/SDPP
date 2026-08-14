using MediatR;
using SDPP.Audit.Application.Ports;
using SDPP.Audit.Domain.Aggregates;
using SDPP.BuildingBlocks.Application;

namespace SDPP.Audit.Application.UseCases.VerifyIntegrity;

public sealed record AuditIntegrityResult(bool IsIntact, int RecordCount, IReadOnlyList<long> BrokenRecordIds);

/// <summary>Backs the internal-only GET /api/v1/audit/records/integrity, consumed by Signature.Api's
/// public envelope verifier (see IAuditClient) to back its "auditoría íntegra" check with a real
/// verification instead of trusting the caller's own copy of events. Unlike QueryTraceQuery's
/// page-local ChainValid (only meaningful over an UNFILTERED page — comparing neighbors within a
/// filtered subset would almost always report broken even when nothing is wrong, since other
/// subjects' records sit between them in the real chain), this checks two things per record that
/// stay valid under filtering: (1) the record's own hash is self-consistent (VerifyHash — catches
/// content tampering), and (2) its PreviousRecordHash matches the RecordHash of the record at
/// Id-1 in the GLOBAL table (catches deletion/reordering of ANY record, not just ones about this
/// subject).</summary>
public sealed record VerifyIntegrityQuery(IReadOnlyList<Guid> SubjectIds) : IQuery<AuditIntegrityResult>;

public sealed class VerifyIntegrityHandler(IAuditRecordRepository repository) : IRequestHandler<VerifyIntegrityQuery, Result<AuditIntegrityResult>>
{
    public async Task<Result<AuditIntegrityResult>> Handle(VerifyIntegrityQuery request, CancellationToken cancellationToken)
    {
        var records = await repository.GetBySubjectIdsAsync(request.SubjectIds, cancellationToken);
        var broken = new List<long>();

        foreach (var record in records)
        {
            if (!record.VerifyHash())
            {
                broken.Add(record.Id);
                continue;
            }

            if (record.Id == 1)
            {
                if (record.PreviousRecordHash != AuditRecord.GenesisHash)
                {
                    broken.Add(record.Id);
                }
                continue;
            }

            var predecessor = await repository.GetByIdAsync(record.Id - 1, cancellationToken);
            if (predecessor is null || predecessor.RecordHash != record.PreviousRecordHash)
            {
                broken.Add(record.Id);
            }
        }

        return Result.Success(new AuditIntegrityResult(broken.Count == 0, records.Count, broken));
    }
}
