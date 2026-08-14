using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Application.Ports;

namespace SDPP.Documents.Application.UseCases.LockDocument;

/// <summary>
/// Internal, service-to-service endpoint consumed by Signature.Api right after it produces the
/// final signed artifact of a completed envelope — never called on a source document. Irreversible:
/// there is no matching Unlock use case (see DocumentInstance.Lock).
/// </summary>
public sealed class LockDocumentHandler(IDocumentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<LockDocumentCommand, Result<LockDocumentResult>>
{
    public async Task<Result<LockDocumentResult>> Handle(LockDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
        {
            return Result.Failure<LockDocumentResult>("El documento no existe.", "DOCUMENT_NOT_FOUND");
        }

        document.Lock();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new LockDocumentResult(document.Id));
    }
}
