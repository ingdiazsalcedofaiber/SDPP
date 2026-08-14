using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.CancelEnvelope;

public sealed record CancelEnvelopeCommand(Guid EnvelopeId) : ICommand;

public sealed class CancelEnvelopeHandler(
    ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<CancelEnvelopeCommand, Result>
{
    public async Task<Result> Handle(CancelEnvelopeCommand request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }
        if (!UseCases.EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()))
        {
            return Result.Failure("No tienes permiso para cancelar este sobre.", "FORBIDDEN");
        }

        try
        {
            envelope.Cancel();
        }
        catch (SDPP.BuildingBlocks.Domain.DomainException ex)
        {
            return Result.Failure(ex.Message, "INVALID_STATE");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new SignatureEnvelopeCancelledV1(
            Guid.NewGuid(), DateTime.UtcNow, envelope.Id, currentActor.UserId),
            cancellationToken);

        return Result.Success();
    }
}
