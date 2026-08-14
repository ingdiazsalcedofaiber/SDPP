using System.Text.Json;
using MassTransit;
using MediatR;
using SDPP.Audit.Application.UseCases.RecordEvent;
using SDPP.Audit.Domain.Aggregates;
using SDPP.BuildingBlocks.Contracts.Documents;

namespace SDPP.Audit.Infrastructure.Messaging;

/// <summary>
/// One MassTransit consumer per integration event the Audit module subscribes to (see
/// docs/01-architecture/c4-diagrams.md — "RabbitMQ → consume todos los eventos de dominio,
/// append-only"). Each simply maps the event onto a RecordEventCommand; the hash-chain logic
/// lives entirely in RecordEventHandler/AuditRecordRepository.
/// </summary>
public sealed class DocumentUploadedConsumer(ISender sender) : IConsumer<DocumentUploadedV1>
{
    public Task Consume(ConsumeContext<DocumentUploadedV1> context) =>
        sender.Send(new RecordEventCommand(
            "DocumentUploaded", context.Message.OccurredAtUtc, ToActorContext(context.Message.Actor),
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));

    internal static ActorContext ToActorContext(ActorSnapshot actor) => new(
        actor.UserId, actor.FullName, actor.Email, actor.Domain,
        actor.IpAddress, actor.MacAddress, actor.Hostname, actor.OperatingSystem, actor.UserAgent);
}

public sealed class ConversionCompletedConsumer(ISender sender) : IConsumer<ConversionCompletedV1>
{
    public Task Consume(ConsumeContext<ConversionCompletedV1> context) =>
        sender.Send(new RecordEventCommand(
            "ConversionCompleted", context.Message.OccurredAtUtc, SystemActor,
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));

    internal static readonly ActorContext SystemActor = new(
        Guid.Empty, "SDPP Conversion Worker", "system@sdpp.internal", "SYSTEM", null, null, null, null, null);
}

public sealed class ConversionFailedConsumer(ISender sender) : IConsumer<ConversionFailedV1>
{
    public Task Consume(ConsumeContext<ConversionFailedV1> context) =>
        sender.Send(new RecordEventCommand(
            "ConversionFailed", context.Message.OccurredAtUtc, ConversionCompletedConsumer.SystemActor,
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));
}

public sealed class DocumentBlockedConsumer(ISender sender) : IConsumer<DocumentBlockedV1>
{
    public Task Consume(ConsumeContext<DocumentBlockedV1> context) =>
        sender.Send(new RecordEventCommand(
            "DocumentBlocked", context.Message.OccurredAtUtc, ConversionCompletedConsumer.SystemActor,
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));
}

/// <summary>More specific than DocumentBlockedConsumer — carries the operation/category context
/// (see docs on protección automática) for both the RequestConversionHandler-time category block
/// (e.g. Historia Clínica → DOCX) and the worker-time blockEditableConversion check (Secreta level).</summary>
public sealed class ConversionBlockedConsumer(ISender sender) : IConsumer<ConversionBlockedV1>
{
    public Task Consume(ConsumeContext<ConversionBlockedV1> context) =>
        sender.Send(new RecordEventCommand(
            "ConversionBlocked", context.Message.OccurredAtUtc, DocumentUploadedConsumer.ToActorContext(context.Message.Actor),
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));
}

public sealed class ProtectionAppliedConsumer(ISender sender) : IConsumer<ProtectionAppliedV1>
{
    public Task Consume(ConsumeContext<ProtectionAppliedV1> context) =>
        sender.Send(new RecordEventCommand(
            "ProtectionApplied", context.Message.OccurredAtUtc, ConversionCompletedConsumer.SystemActor,
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));
}

/// <summary>Records the "notificar al administrador" requirement for Altamente Sensible/Secreta
/// documents — see AuditLoggingNotificationSender, the default INotificationSender, which is what
/// actually publishes AdminNotificationRequestedV1 until a real email/Teams/Slack channel exists.</summary>
public sealed class AdminNotificationRequestedConsumer(ISender sender) : IConsumer<AdminNotificationRequestedV1>
{
    public Task Consume(ConsumeContext<AdminNotificationRequestedV1> context) =>
        sender.Send(new RecordEventCommand(
            "AdminNotificationRequested", context.Message.OccurredAtUtc, ConversionCompletedConsumer.SystemActor,
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));
}

/// <summary>Records every fingerprint/classification decision a document's first-ever inspection
/// makes — whether freshly computed or inherited from a previously-seen DocumentVersion (see
/// IChangeDetectionService) — satisfying the "nunca eliminar el historial de clasificación"
/// requirement from the integrity-and-fingerprint proposal.</summary>
public sealed class DocumentVersionCreatedConsumer(ISender sender) : IConsumer<DocumentVersionCreatedV1>
{
    public Task Consume(ConsumeContext<DocumentVersionCreatedV1> context) =>
        sender.Send(new RecordEventCommand(
            "DocumentVersionCreated", context.Message.OccurredAtUtc, DocumentUploadedConsumer.ToActorContext(context.Message.Actor),
            context.Message.DocumentId, JsonSerializer.Serialize(context.Message)));
}

/// <summary>Six consumers for the multi-signer envelope module (replaces the old single-signer
/// DocumentSignedConsumer). PayloadJson (the full serialized event) already carries every field the
/// spec's audit section requires — usuario/correo/rol, fecha/hora, IP/navegador, hash antes/
/// después, método de auth/firma, estado — no new AuditRecord columns needed, same discipline as
/// every other consumer here. SubjectDocumentId is the most specific artifact each event actually
/// concerns: the source document when it's known up front (Sent), the final signed artifact once it
/// exists (Completed), and the envelope id itself for recipient-level events that happen before any
/// document artifact is touched (Viewed/Signed-in-progress/Declined/Cancelled).</summary>
public sealed class SignatureEnvelopeSentConsumer(ISender sender) : IConsumer<SignatureEnvelopeSentV1>
{
    public Task Consume(ConsumeContext<SignatureEnvelopeSentV1> context) =>
        sender.Send(new RecordEventCommand(
            "SignatureEnvelopeSent", context.Message.OccurredAtUtc, DocumentUploadedConsumer.ToActorContext(context.Message.Actor),
            context.Message.SourceDocumentId, JsonSerializer.Serialize(context.Message)));
}

public sealed class EnvelopeRecipientViewedConsumer(ISender sender) : IConsumer<EnvelopeRecipientViewedV1>
{
    public Task Consume(ConsumeContext<EnvelopeRecipientViewedV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(Guid.Empty, m.RecipientEmail, m.RecipientEmail, string.Empty, m.IpAddress, null, null, null, m.UserAgent);
        return sender.Send(new RecordEventCommand("EnvelopeRecipientViewed", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}

public sealed class EnvelopeRecipientSignedConsumer(ISender sender) : IConsumer<EnvelopeRecipientSignedV1>
{
    public Task Consume(ConsumeContext<EnvelopeRecipientSignedV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(
            m.MatchedUserId ?? Guid.Empty, m.RecipientFullName, m.RecipientEmail, string.Empty, m.IpAddress, null, null, null, m.UserAgent);
        return sender.Send(new RecordEventCommand("EnvelopeRecipientSigned", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}

public sealed class EnvelopeRecipientDeclinedConsumer(ISender sender) : IConsumer<EnvelopeRecipientDeclinedV1>
{
    public Task Consume(ConsumeContext<EnvelopeRecipientDeclinedV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(Guid.Empty, m.RecipientEmail, m.RecipientEmail, string.Empty, m.IpAddress, null, null, null, m.UserAgent);
        return sender.Send(new RecordEventCommand("EnvelopeRecipientDeclined", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}

public sealed class SignatureEnvelopeCompletedConsumer(ISender sender) : IConsumer<SignatureEnvelopeCompletedV1>
{
    public Task Consume(ConsumeContext<SignatureEnvelopeCompletedV1> context) =>
        sender.Send(new RecordEventCommand(
            "SignatureEnvelopeCompleted", context.Message.OccurredAtUtc, ConversionCompletedConsumer.SystemActor,
            context.Message.FinalDocumentId, JsonSerializer.Serialize(context.Message)));
}

public sealed class SignatureEnvelopeCancelledConsumer(ISender sender) : IConsumer<SignatureEnvelopeCancelledV1>
{
    public Task Consume(ConsumeContext<SignatureEnvelopeCancelledV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(m.CancelledByUserId, string.Empty, string.Empty, string.Empty, null, null, null, null, null);
        return sender.Send(new RecordEventCommand("SignatureEnvelopeCancelled", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}

public sealed class SignatureEnvelopeDocumentAttachedConsumer(ISender sender) : IConsumer<SignatureEnvelopeDocumentAttachedV1>
{
    public Task Consume(ConsumeContext<SignatureEnvelopeDocumentAttachedV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(m.CreatedByUserId, string.Empty, string.Empty, string.Empty, null, null, null, null, null);
        return sender.Send(new RecordEventCommand("SignatureEnvelopeDocumentAttached", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}

public sealed class RecipientOtpRequestedConsumer(ISender sender) : IConsumer<RecipientOtpRequestedV1>
{
    public Task Consume(ConsumeContext<RecipientOtpRequestedV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(Guid.Empty, m.RecipientEmail, m.RecipientEmail, string.Empty, m.IpAddress, null, null, null, m.UserAgent);
        return sender.Send(new RecordEventCommand("RecipientOtpRequested", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}

public sealed class RecipientOtpValidatedConsumer(ISender sender) : IConsumer<RecipientOtpValidatedV1>
{
    public Task Consume(ConsumeContext<RecipientOtpValidatedV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(Guid.Empty, m.RecipientEmail, m.RecipientEmail, string.Empty, m.IpAddress, null, null, null, m.UserAgent);
        return sender.Send(new RecordEventCommand("RecipientOtpValidated", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}

/// <summary>Not yet published by anything (see SignatureEnvelopeExpiredV1's doc comment) — the
/// consumer is registered now so it's ready the moment the Fase I lifecycle job starts firing it.</summary>
public sealed class SignatureEnvelopeExpiredConsumer(ISender sender) : IConsumer<SignatureEnvelopeExpiredV1>
{
    public Task Consume(ConsumeContext<SignatureEnvelopeExpiredV1> context) =>
        sender.Send(new RecordEventCommand(
            "SignatureEnvelopeExpired", context.Message.OccurredAtUtc, ConversionCompletedConsumer.SystemActor,
            context.Message.EnvelopeId, JsonSerializer.Serialize(context.Message)));
}

public sealed class CertificateGeneratedConsumer(ISender sender) : IConsumer<CertificateGeneratedV1>
{
    public Task Consume(ConsumeContext<CertificateGeneratedV1> context) =>
        sender.Send(new RecordEventCommand(
            "CertificateGenerated", context.Message.OccurredAtUtc, ConversionCompletedConsumer.SystemActor,
            context.Message.EnvelopeId, JsonSerializer.Serialize(context.Message)));
}

public sealed class EnvelopeVerificationPerformedConsumer(ISender sender) : IConsumer<EnvelopeVerificationPerformedV1>
{
    public Task Consume(ConsumeContext<EnvelopeVerificationPerformedV1> context)
    {
        var m = context.Message;
        var actor = new ActorContext(Guid.Empty, "Verificador público", "verificador@sdpp.internal", string.Empty, m.IpAddress, null, null, null, m.UserAgent);
        return sender.Send(new RecordEventCommand("EnvelopeVerificationPerformed", m.OccurredAtUtc, actor, m.EnvelopeId, JsonSerializer.Serialize(m)));
    }
}
