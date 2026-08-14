using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

public sealed record DeclineEnvelopeCommand(string RawToken, string? SessionToken, string Reason) : ICommand;

public sealed class DeclineEnvelopeHandler(
    ISignatureEnvelopeRepository envelopeRepository,
    ISignerAccessChallengeRepository challengeRepository,
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher,
    INotificationRepository notificationRepository)
    : IRequestHandler<DeclineEnvelopeCommand, Result>
{
    public async Task<Result> Handle(DeclineEnvelopeCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawToken)));
        var challenge = await challengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null || !challenge.IsLinkUsable)
        {
            return Result.Failure("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var envelope = await envelopeRepository.GetByRecipientIdAsync(challenge.RecipientId, cancellationToken);
        var recipient = envelope?.Recipients.FirstOrDefault(r => r.Id == challenge.RecipientId);
        if (envelope is null || recipient is null)
        {
            return Result.Failure("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }
        if (!RecipientAccessAuthorization.CanAct(recipient, challenge, currentActor, request.SessionToken))
        {
            return Result.Failure("No estás autenticado para actuar como este destinatario.", "FORBIDDEN");
        }

        try
        {
            envelope.RegisterDecline(recipient.Id, request.Reason);
        }
        catch (SDPP.BuildingBlocks.Domain.DomainException ex)
        {
            return Result.Failure(ex.Message, "INVALID_STATE");
        }

        notificationRepository.Add(SDPP.Signature.Domain.Aggregates.InAppNotification.Create(
            envelope.CreatedByUserId, SDPP.Signature.Domain.Enums.NotificationType.EnvelopeDeclined,
            "Firmante rechazó el sobre", $"{recipient.FullName} rechazó firmar \"{envelope.Title}\": {request.Reason}", envelope.Id));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new EnvelopeRecipientDeclinedV1(
            Guid.NewGuid(), DateTime.UtcNow, envelope.Id, recipient.Id, recipient.Email, request.Reason,
            currentActor.IpAddress, currentActor.UserAgent),
            cancellationToken);

        return Result.Success();
    }
}
