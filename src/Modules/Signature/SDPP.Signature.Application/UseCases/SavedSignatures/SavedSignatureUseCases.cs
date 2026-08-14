using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Application.UseCases.SavedSignatures;

public sealed record SavedSignatureDto(Guid Id, string Label, double AspectRatio, DateTime CreatedAtUtc);

public sealed record AddSavedSignatureResult(Guid Id);
public sealed record AddSavedSignatureCommand(byte[] ImageBytes, double AspectRatio, string Label) : ICommand<AddSavedSignatureResult>;

public sealed record ListSavedSignaturesQuery : IQuery<IReadOnlyList<SavedSignatureDto>>;

public sealed record DeleteSavedSignatureCommand(Guid Id) : ICommand;

public sealed record SavedSignatureImage(byte[] ImageBytes);
public sealed record GetSavedSignatureImageQuery(Guid Id) : IQuery<SavedSignatureImage>;

public sealed class GetSavedSignatureImageHandler(ISavedSignatureRepository repository, ICurrentActor currentActor)
    : IRequestHandler<GetSavedSignatureImageQuery, Result<SavedSignatureImage>>
{
    public async Task<Result<SavedSignatureImage>> Handle(GetSavedSignatureImageQuery request, CancellationToken cancellationToken)
    {
        var signature = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (signature is null || signature.UserId != currentActor.UserId)
        {
            return Result.Failure<SavedSignatureImage>("La firma guardada no existe.", "SAVED_SIGNATURE_NOT_FOUND");
        }
        return Result.Success(new SavedSignatureImage(signature.ImageBytes));
    }
}

public sealed class AddSavedSignatureHandler(ISavedSignatureRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor)
    : IRequestHandler<AddSavedSignatureCommand, Result<AddSavedSignatureResult>>
{
    public async Task<Result<AddSavedSignatureResult>> Handle(AddSavedSignatureCommand request, CancellationToken cancellationToken)
    {
        var signature = SavedSignature.Create(currentActor.UserId, request.ImageBytes, request.AspectRatio, request.Label);
        repository.Add(signature);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new AddSavedSignatureResult(signature.Id));
    }
}

public sealed class ListSavedSignaturesHandler(ISavedSignatureRepository repository, ICurrentActor currentActor)
    : IRequestHandler<ListSavedSignaturesQuery, Result<IReadOnlyList<SavedSignatureDto>>>
{
    public async Task<Result<IReadOnlyList<SavedSignatureDto>>> Handle(ListSavedSignaturesQuery request, CancellationToken cancellationToken)
    {
        var signatures = await repository.GetByUserIdAsync(currentActor.UserId, cancellationToken);
        return Result.Success<IReadOnlyList<SavedSignatureDto>>(
            signatures.Select(s => new SavedSignatureDto(s.Id, s.Label, s.AspectRatio, s.CreatedAtUtc)).ToList());
    }
}

public sealed class DeleteSavedSignatureHandler(ISavedSignatureRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor)
    : IRequestHandler<DeleteSavedSignatureCommand, Result>
{
    public async Task<Result> Handle(DeleteSavedSignatureCommand request, CancellationToken cancellationToken)
    {
        var signature = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (signature is null || signature.UserId != currentActor.UserId)
        {
            return Result.Failure("La firma guardada no existe.", "SAVED_SIGNATURE_NOT_FOUND");
        }

        repository.Remove(signature);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
