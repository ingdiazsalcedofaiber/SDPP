using FluentValidation;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Application.UseCases.FindDocumentByHash;

public sealed record FindDocumentByHashResult(Guid DocumentId, Guid DocumentVersionId);

/// <summary>
/// Resolves a SHA-256 hash to the document it belongs to — exact match only. Backs the audit
/// trail's "buscar por hash" flow: the frontend resolves hash → DocumentId here, then queries
/// Audit.Api's existing (unchanged) documentId filter to show the related records.
/// </summary>
public sealed record FindDocumentByHashQuery(string Sha256Hash) : IQuery<FindDocumentByHashResult>;

public sealed class FindDocumentByHashValidator : AbstractValidator<FindDocumentByHashQuery>
{
    public FindDocumentByHashValidator()
    {
        RuleFor(q => q.Sha256Hash)
            .NotEmpty()
            .Matches("^[a-fA-F0-9]{64}$")
            .WithMessage("El hash SHA-256 debe tener exactamente 64 caracteres hexadecimales.");
    }
}

public sealed class FindDocumentByHashHandler(IDocumentIntegrityRecordRepository repository)
    : IRequestHandler<FindDocumentByHashQuery, Result<FindDocumentByHashResult>>
{
    public async Task<Result<FindDocumentByHashResult>> Handle(FindDocumentByHashQuery request, CancellationToken cancellationToken)
    {
        var record = await repository.GetByHashAsync(request.Sha256Hash.ToLowerInvariant(), cancellationToken);

        return record is null
            ? Result.Failure<FindDocumentByHashResult>("No existe ningún documento con ese hash.", "HASH_NOT_FOUND")
            : Result.Success(new FindDocumentByHashResult(record.DocumentId, record.DocumentVersionId));
    }
}
