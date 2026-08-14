using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.ConfigureEnvelope;

public sealed record AddFieldResult(Guid FieldId);

public sealed record AddFieldCommand(
    Guid EnvelopeId, Guid RecipientId, FieldType Type, int PageNumber,
    double PositionX, double PositionY, double Width, double Height, bool Required)
    : ICommand<AddFieldResult>;

public sealed class AddFieldHandler(
    ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork, ICurrentActor currentActor,
    IOrganizationContextProvider organizationContextProvider, ILegalApprovalStampPolicy legalApprovalStampPolicy)
    : IRequestHandler<AddFieldCommand, Result<AddFieldResult>>
{
    public async Task<Result<AddFieldResult>> Handle(AddFieldCommand request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<AddFieldResult>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }
        if (!EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()))
        {
            return Result.Failure<AddFieldResult>("No tienes permiso para modificar este sobre.", "FORBIDDEN");
        }

        // Early feedback only — the actually-enforced check is in CompleteRecipientSigningCommand,
        // since a recipient's email could still change (edge case: re-adding the same recipient
        // with a different email) between field creation and signing.
        if (request.Type == FieldType.LegalApprovalStamp)
        {
            var targetRecipient = envelope.Recipients.FirstOrDefault(r => r.Id == request.RecipientId);
            if (targetRecipient is null || !legalApprovalStampPolicy.IsAuthorized(targetRecipient.Email))
            {
                return Result.Failure<AddFieldResult>(
                    "El sello de aprobación legal solo puede asignarse al firmante autorizado de Gerencia Legal.", "FORBIDDEN");
            }
        }

        var field = envelope.AddField(
            request.RecipientId, request.Type, request.PageNumber,
            request.PositionX, request.PositionY, request.Width, request.Height, request.Required);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new AddFieldResult(field.Id));
    }
}
