using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.GetEnvelope;

public sealed record RecipientDetailDto(
    Guid RecipientId, int Order, string Email, string FullName, Guid? MatchedUserId, string Status,
    DateTime? SentAtUtc, DateTime? ViewedAtUtc, DateTime? SignedAtUtc, DateTime? DeclinedAtUtc, string? DeclineReason);

public sealed record FieldDetailDto(
    Guid FieldId, Guid RecipientId, FieldType Type, int PageNumber,
    double PositionX, double PositionY, double Width, double Height, bool Required, bool IsFilled, string? SignatureHash);

public sealed record EnvelopeDetailDto(
    Guid EnvelopeId, Guid SourceDocumentId, string Title, string? Message, Guid CreatedByUserId, DateTime CreatedAtUtc,
    string SigningMode, DateTime? DueDateUtc, string Status, DateTime? SentAtUtc, DateTime? CompletedAtUtc,
    string OriginalSha256Hash, Guid? FinalDocumentId, Guid? FinalDocumentVersionId, string? FinalSha256Hash, string? EnvelopeHash,
    IReadOnlyList<RecipientDetailDto> Recipients, IReadOnlyList<FieldDetailDto> Fields);

/// <summary>Full, unscoped detail — for the creator/an administrator, not the public signer-access
/// flow (see ViewEnvelopeAccessQuery for the recipient-scoped equivalent).</summary>
public sealed record GetEnvelopeQuery(Guid EnvelopeId) : IQuery<EnvelopeDetailDto>;

public sealed class GetEnvelopeHandler(
    ISignatureEnvelopeRepository repository, ICurrentActor currentActor, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<GetEnvelopeQuery, Result<EnvelopeDetailDto>>
{
    public async Task<Result<EnvelopeDetailDto>> Handle(GetEnvelopeQuery request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<EnvelopeDetailDto>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }

        var isRecipient = envelope.Recipients.Any(r => r.MatchedUserId == currentActor.UserId);
        if (!UseCases.EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()) && !isRecipient)
        {
            return Result.Failure<EnvelopeDetailDto>("No tienes permiso para ver este sobre.", "FORBIDDEN");
        }

        var recipients = envelope.Recipients
            .Select(r => new RecipientDetailDto(
                r.Id, r.Order, r.Email, r.FullName, r.MatchedUserId, r.Status.ToString(),
                r.SentAtUtc, r.ViewedAtUtc, r.SignedAtUtc, r.DeclinedAtUtc, r.DeclineReason))
            .ToList();

        var fields = envelope.Fields
            .Select(f => new FieldDetailDto(
                f.Id, f.RecipientId, f.Type, f.PageNumber, f.PositionX, f.PositionY, f.Width, f.Height, f.Required, f.IsFilled, f.SignatureHash))
            .ToList();

        return Result.Success(new EnvelopeDetailDto(
            envelope.Id, envelope.SourceDocumentId, envelope.Title, envelope.Message, envelope.CreatedByUserId, envelope.CreatedAtUtc,
            envelope.SigningMode.ToString(), envelope.DueDateUtc, envelope.Status.ToString(), envelope.SentAtUtc, envelope.CompletedAtUtc,
            envelope.OriginalSha256Hash, envelope.FinalDocumentId, envelope.FinalDocumentVersionId, envelope.FinalSha256Hash, envelope.EnvelopeHash,
            recipients, fields));
    }
}
