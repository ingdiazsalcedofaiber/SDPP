using System.Security.Cryptography;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.CreateEnvelope;

public sealed record CreateEnvelopeResult(Guid EnvelopeId);

public sealed record CreateEnvelopeCommand(
    Guid SourceDocumentId, string Title, string? Message, SigningMode SigningMode, DateTime? DueDateUtc)
    : ICommand<CreateEnvelopeResult>;

/// <summary>Creates a Draft envelope — downloads the source PDF once just to establish
/// OriginalSha256Hash and SourceDocumentVersionId up front (same hash-inline discipline as
/// UploadDocumentHandler); recipients and fields are added afterward via ConfigureEnvelope.</summary>
public sealed class CreateEnvelopeHandler(
    IDocumentsClient documentsClient, ISignatureEnvelopeRepository repository, IUnitOfWork unitOfWork,
    ICurrentActor currentActor, IIntegrationEventPublisher integrationEventPublisher, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<CreateEnvelopeCommand, Result<CreateEnvelopeResult>>
{
    public async Task<Result<CreateEnvelopeResult>> Handle(CreateEnvelopeCommand request, CancellationToken cancellationToken)
    {
        var original = await documentsClient.DownloadAsync(request.SourceDocumentId, cancellationToken);
        string hashHex;
        await using (original.Content)
        {
            hashHex = Convert.ToHexStringLower(await SHA256.HashDataAsync(original.Content, cancellationToken));
        }

        var envelope = SignatureEnvelope.Create(
            request.SourceDocumentId, original.DocumentVersionId, request.Title, request.Message,
            currentActor.UserId, request.SigningMode, request.DueDateUtc, hashHex, organizationContextProvider.GetCurrentOrganizationId());

        repository.Add(envelope);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new SignatureEnvelopeDocumentAttachedV1(
            Guid.NewGuid(), DateTime.UtcNow, envelope.Id, request.SourceDocumentId, hashHex, currentActor.UserId),
            cancellationToken);

        return Result.Success(new CreateEnvelopeResult(envelope.Id));
    }
}
