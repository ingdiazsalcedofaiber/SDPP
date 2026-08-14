using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Application.UseCases.GetBatchIntegrity;

public sealed record DocumentIntegrityDto(Guid DocumentId, string? IntegritySignature, IReadOnlyList<string> ProtectionsApplied);

public sealed record GetBatchIntegrityQuery(IReadOnlyList<Guid> DocumentIds) : IQuery<IReadOnlyList<DocumentIntegrityDto>>;

/// <summary>Batched lookup (avoids N+1) for Documents.Api's GetDocumentStatusHandler, which needs
/// the integrity signature/protections-applied of every job's output document in one call — see
/// the "Clasificación de Activos de Información" extraction.</summary>
public sealed class GetBatchIntegrityHandler(IDocumentIntegrityRecordRepository repository)
    : IRequestHandler<GetBatchIntegrityQuery, Result<IReadOnlyList<DocumentIntegrityDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentIntegrityDto>>> Handle(GetBatchIntegrityQuery request, CancellationToken cancellationToken)
    {
        var records = await repository.GetByDocumentIdsAsync(request.DocumentIds, cancellationToken);

        IReadOnlyList<DocumentIntegrityDto> dtos = records
            .Select(r => new DocumentIntegrityDto(r.DocumentId, r.IntegritySignature, r.ProtectionsApplied))
            .ToList();

        return Result.Success(dtos);
    }
}
