using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Application.UseCases.GetVersionSummary;

public sealed record VersionSummaryResult(
    string? Classification, int? RiskScore, string? Category, IReadOnlyList<string> Labels);

public sealed record GetVersionSummaryQuery(Guid DocumentVersionId) : IQuery<VersionSummaryResult>;

/// <summary>
/// Lets Documents.Api enrich GET /api/v1/documents/{id} with the classification Classification.Api
/// now owns, without the frontend contract changing at all — see the "Clasificación de Activos de
/// Información" extraction. A missing record (async consumer hasn't landed the fingerprint yet)
/// returns nulls rather than failing — the frontend already polls status, so a transient null
/// self-heals on the next poll.
/// </summary>
public sealed class GetVersionSummaryHandler(IDocumentVersionFingerprintRepository repository)
    : IRequestHandler<GetVersionSummaryQuery, Result<VersionSummaryResult>>
{
    public async Task<Result<VersionSummaryResult>> Handle(GetVersionSummaryQuery request, CancellationToken cancellationToken)
    {
        var record = await repository.GetByDocumentVersionIdAsync(request.DocumentVersionId, cancellationToken);

        return Result.Success(record is null
            ? new VersionSummaryResult(null, null, null, [])
            : new VersionSummaryResult(record.Classification.ToString(), record.RiskScore, record.Category, record.Labels));
    }
}
