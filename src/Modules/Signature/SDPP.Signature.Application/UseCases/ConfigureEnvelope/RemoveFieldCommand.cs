using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.ConfigureEnvelope;

public sealed record RemoveFieldCommand(Guid EnvelopeId, Guid FieldId) : ICommand;

public sealed class RemoveFieldHandler(
    ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor,
    IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<RemoveFieldCommand, Result>
{
    public async Task<Result> Handle(RemoveFieldCommand request, CancellationToken cancellationToken)
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

        envelope.RemoveField(request.FieldId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
