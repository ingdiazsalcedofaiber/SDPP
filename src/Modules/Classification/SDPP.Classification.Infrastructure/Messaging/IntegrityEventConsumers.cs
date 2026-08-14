using MassTransit;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Classification.Application.Ports;
using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.ValueObjects;

namespace SDPP.Classification.Infrastructure.Messaging;

/// <summary>
/// Populates DocumentIntegrityRecord asynchronously from the exact same integration events Audit
/// already consumes (see AuditEventConsumers.cs) — no new event types needed beyond a few
/// additive fields, just new consumers of existing ones. Hashing/protection still execute where
/// the file bytes already are (Documents.Application/Conversion Worker); this module owns what
/// the resulting values mean and where they live at rest, per the "Clasificación de Activos de
/// Información" extraction.
/// </summary>
public sealed class DocumentUploadedIntegrityConsumer(
    IDocumentIntegrityRecordRepository repository, IUnitOfWork unitOfWork) : IConsumer<DocumentUploadedV1>
{
    public async Task Consume(ConsumeContext<DocumentUploadedV1> context)
    {
        var message = context.Message;
        if (await repository.GetByDocumentIdAsync(message.DocumentId, context.CancellationToken) is not null)
        {
            return; // already registered (duplicate delivery)
        }

        var record = DocumentIntegrityRecord.Register(
            message.DocumentId, message.DocumentVersionId, message.ContentType, FileHash.FromHex(message.Sha256Hash));
        repository.Add(record);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class ConversionCompletedIntegrityConsumer(
    IDocumentIntegrityRecordRepository repository, IUnitOfWork unitOfWork) : IConsumer<ConversionCompletedV1>
{
    public async Task Consume(ConsumeContext<ConversionCompletedV1> context)
    {
        var message = context.Message;
        if (await repository.GetByDocumentIdAsync(message.OutputDocumentId, context.CancellationToken) is not null)
        {
            // Either a duplicate delivery, or ProtectionAppliedIntegrityConsumer already created
            // this record because ProtectionAppliedV1 happened to arrive first — bus delivery
            // order between two independently-published event types is never guaranteed.
            return;
        }

        var record = DocumentIntegrityRecord.Register(
            message.OutputDocumentId, message.DocumentVersionId, message.OutputContentType, FileHash.FromHex(message.OutputSha256Hash));
        repository.Add(record);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class ProtectionAppliedIntegrityConsumer(
    IDocumentIntegrityRecordRepository repository, IUnitOfWork unitOfWork) : IConsumer<ProtectionAppliedV1>
{
    public async Task Consume(ConsumeContext<ProtectionAppliedV1> context)
    {
        var message = context.Message;
        var record = await repository.GetByDocumentIdAsync(message.OutputDocumentId, context.CancellationToken);

        if (record is null)
        {
            // Raced ahead of ConversionCompletedIntegrityConsumer — create the record now with
            // what this event carries; the other consumer becomes a same-record no-op once it lands.
            record = DocumentIntegrityRecord.Register(
                message.OutputDocumentId, message.DocumentVersionId, message.OutputContentType, FileHash.FromHex(message.OutputSha256Hash));
            repository.Add(record);
        }

        if (message.IntegritySignature is not null)
        {
            record.SetIntegritySignature(message.IntegritySignature);
        }
        if (message.ProtectionsApplied.Count > 0)
        {
            record.SetProtectionsApplied(message.ProtectionsApplied);
        }

        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>Populates DocumentIntegrityRecord for the final artifact of a completed multi-signer
/// envelope, and copies (never recomputes) the source version's fingerprint/classification onto the
/// new signed version — signing is not a reclassification event, so this is an inherit-copy exactly
/// like DocumentVersionFingerprint.InheritClassificationFrom already does for fingerprint-matched
/// content, not a real inspection. Replaces the old single-signer DocumentSignedIntegrityConsumer.</summary>
public sealed class SignatureEnvelopeCompletedIntegrityConsumer(
    IDocumentIntegrityRecordRepository integrityRepository,
    IDocumentVersionFingerprintRepository fingerprintRepository,
    IUnitOfWork unitOfWork) : IConsumer<SignatureEnvelopeCompletedV1>
{
    public async Task Consume(ConsumeContext<SignatureEnvelopeCompletedV1> context)
    {
        var message = context.Message;

        if (await integrityRepository.GetByDocumentIdAsync(message.FinalDocumentId, context.CancellationToken) is null)
        {
            integrityRepository.Add(DocumentIntegrityRecord.Register(
                message.FinalDocumentId, message.FinalDocumentVersionId, "application/pdf",
                FileHash.FromHex(message.FinalSha256Hash)));
        }

        if (await fingerprintRepository.GetByDocumentVersionIdAsync(message.FinalDocumentVersionId, context.CancellationToken) is null)
        {
            var sourceFingerprint = await fingerprintRepository.GetByDocumentVersionIdAsync(message.SourceDocumentVersionId, context.CancellationToken);
            if (sourceFingerprint is not null)
            {
                var newFingerprint = DocumentVersionFingerprint.Create(message.FinalDocumentVersionId, sourceFingerprint.LogicalDocumentId);
                newFingerprint.InheritClassificationFrom(sourceFingerprint);
                fingerprintRepository.Add(newFingerprint);
            }
        }

        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
