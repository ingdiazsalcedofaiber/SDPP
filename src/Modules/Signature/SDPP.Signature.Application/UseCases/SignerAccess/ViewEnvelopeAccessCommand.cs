using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

public sealed record AccessFieldDto(
    Guid FieldId, FieldType Type, int PageNumber, double PositionX, double PositionY,
    double Width, double Height, bool Required, bool IsFilled);

public sealed record EnvelopeAccessView(
    Guid EnvelopeId, string Title, string? Message, Guid SourceDocumentId, string EnvelopeStatus,
    Guid RecipientId, string RecipientEmail, string RecipientFullName, string RecipientStatus,
    bool RequiresOtp, bool RequiresSdppLogin, Guid? MatchedUserId, bool ConsentAccepted, IReadOnlyList<AccessFieldDto> Fields);

/// <summary>Backs GET /access/{token} — the entry point of the public signing flow. Marks the
/// recipient Viewed (first time only) and reports whether this recipient needs an OTP (no SDPP
/// account) or just a valid SDPP session for the matched account (has one). Scoped strictly to this
/// recipient's own fields — never exposes other recipients' data, even though they share the same
/// envelope.</summary>
public sealed record ViewEnvelopeAccessCommand(string RawToken) : ICommand<EnvelopeAccessView>;

public sealed class ViewEnvelopeAccessHandler(
    ISignerAccessChallengeRepository challengeRepository,
    ISignatureEnvelopeRepository envelopeRepository,
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<ViewEnvelopeAccessCommand, Result<EnvelopeAccessView>>
{
    public async Task<Result<EnvelopeAccessView>> Handle(ViewEnvelopeAccessCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawToken)));
        var challenge = await challengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null || !challenge.IsLinkUsable)
        {
            return Result.Failure<EnvelopeAccessView>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var envelope = await envelopeRepository.GetByRecipientIdAsync(challenge.RecipientId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<EnvelopeAccessView>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var recipient = envelope.Recipients.First(r => r.Id == challenge.RecipientId);
        var wasFirstView = recipient.ViewedAtUtc is null;

        if (envelope.Status is not (EnvelopeStatus.Completed or EnvelopeStatus.Declined or EnvelopeStatus.Cancelled or EnvelopeStatus.Expired)
            && recipient.Status is RecipientStatus.Sent or RecipientStatus.Viewed)
        {
            envelope.RegisterView(recipient.Id, currentActor.IpAddress);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (wasFirstView)
            {
                await integrationEventPublisher.PublishAsync(new EnvelopeRecipientViewedV1(
                    Guid.NewGuid(), DateTime.UtcNow, envelope.Id, recipient.Id, recipient.Email,
                    currentActor.IpAddress, currentActor.UserAgent),
                    cancellationToken);
            }
        }

        var fields = envelope.Fields
            .Where(f => f.RecipientId == recipient.Id)
            .Select(f => new AccessFieldDto(f.Id, f.Type, f.PageNumber, f.PositionX, f.PositionY, f.Width, f.Height, f.Required, f.IsFilled))
            .ToList();

        return Result.Success(new EnvelopeAccessView(
            envelope.Id, envelope.Title, envelope.Message, envelope.SourceDocumentId, envelope.Status.ToString(),
            recipient.Id, recipient.Email, recipient.FullName, recipient.Status.ToString(),
            RequiresOtp: recipient.MatchedUserId is null, RequiresSdppLogin: recipient.MatchedUserId is not null,
            recipient.MatchedUserId, ConsentAccepted: recipient.ConsentAcceptedAtUtc is not null, fields));
    }
}
