using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.ConfigureEnvelope;

public sealed record RemoveRecipientCommand(Guid EnvelopeId, Guid RecipientId) : ICommand;

public sealed class RemoveRecipientHandler(
    ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor,
    IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<RemoveRecipientCommand, Result>
{
    public async Task<Result> Handle(RemoveRecipientCommand request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }
        if (!EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()))
        {
            return Result.Failure("No tienes permiso para modificar este sobre.", "FORBIDDEN");
        }

        envelope.RemoveRecipient(request.RecipientId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
