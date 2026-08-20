using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.ConfigureEnvelope;

public sealed record AddRecipientResult(Guid RecipientId);

public sealed record AddRecipientCommand(Guid EnvelopeId, string Email, string FullName, int Order, bool InPerson = false) : ICommand<AddRecipientResult>;

public sealed class AddRecipientHandler(
    ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor,
    IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<AddRecipientCommand, Result<AddRecipientResult>>
{
    public async Task<Result<AddRecipientResult>> Handle(AddRecipientCommand request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<AddRecipientResult>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }
        if (!EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()))
        {
            return Result.Failure<AddRecipientResult>("No tienes permiso para modificar este sobre.", "FORBIDDEN");
        }

        var recipient = envelope.AddRecipient(request.Email, request.FullName, request.Order, request.InPerson);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new AddRecipientResult(recipient.Id));
    }
}
