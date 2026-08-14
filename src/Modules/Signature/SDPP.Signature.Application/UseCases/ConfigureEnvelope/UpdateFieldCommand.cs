using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.ConfigureEnvelope;

public sealed record UpdateFieldCommand(
    Guid EnvelopeId, Guid FieldId, double PositionX, double PositionY, double Width, double Height) : ICommand;

public sealed class UpdateFieldHandler(
    ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor,
    IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<UpdateFieldCommand, Result>
{
    public async Task<Result> Handle(UpdateFieldCommand request, CancellationToken cancellationToken)
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

        envelope.UpdateField(request.FieldId, request.PositionX, request.PositionY, request.Width, request.Height);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
