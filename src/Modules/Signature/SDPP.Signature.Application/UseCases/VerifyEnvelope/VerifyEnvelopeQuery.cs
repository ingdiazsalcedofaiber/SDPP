using System.Security.Cryptography;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.VerifyEnvelope;

public sealed record VerificationRecipientSummary(string FullName, string Email, RecipientStatus Status, DateTime? SignedAtUtc);

public sealed record EnvelopeVerificationResult(
    Guid EnvelopeId, string Title, EnvelopeStatus Status, DateTime? CompletedAtUtc,
    bool IsIntact, bool DocumentHashMatches, bool CertificateHashMatches, bool AuditTrailIntact, int AuditRecordCount,
    string OriginalSha256Hash, string? FinalSha256Hash, string? EnvelopeHash, string? CertificateHash,
    IReadOnlyList<VerificationRecipientSummary> Recipients);

/// <summary>
/// Backs GET /api/v1/signature/verify/{envelopeId} — the ONLY endpoint in this module that is both
/// AllowAnonymous AND takes no per-recipient token/OTP at all, by design: it's meant to be reachable
/// by anyone scanning the QR code printed on a completion certificate, with nothing more than the
/// envelope's own (already-public-once-you-have-the-PDF) ID. Deliberately scoped to integrity
/// evidence only — no recipient IP addresses, no field contents, nothing beyond what a certificate
/// holder could already see printed on the certificate itself. Recomputes the final document's own
/// hash from the bytes actually stored today and compares against what was recorded at completion
/// time — the one check that can catch the final PDF having been silently swapped/corrupted at rest,
/// which no amount of trusting the database row alone could catch.
/// </summary>
public sealed record VerifyEnvelopeQuery(Guid EnvelopeId) : IQuery<EnvelopeVerificationResult>;

public sealed class VerifyEnvelopeHandler(
    ISignatureEnvelopeRepository repository, IDocumentsClient documentsClient, IAuditClient auditClient,
    ICurrentActor currentActor, IIntegrationEventPublisher integrationEventPublisher)
    : IRequestHandler<VerifyEnvelopeQuery, Result<EnvelopeVerificationResult>>
{
    public async Task<Result<EnvelopeVerificationResult>> Handle(VerifyEnvelopeQuery request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<EnvelopeVerificationResult>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }

        var recipients = envelope.Recipients
            .OrderBy(r => r.Order)
            .Select(r => new VerificationRecipientSummary(r.FullName, r.Email, r.Status, r.SignedAtUtc))
            .ToList();

        // Every id this envelope's evidence could plausibly have been filed under across its
        // lifecycle (see AuditEventConsumers' doc comment: SubjectDocumentId varies by event type) —
        // Audit's integrity check needs the full set to find every relevant record.
        var subjectIds = new[] { envelope.Id, envelope.SourceDocumentId, envelope.FinalDocumentId ?? Guid.Empty }
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var auditIntegrity = await auditClient.VerifyIntegrityAsync(subjectIds, cancellationToken);

        EnvelopeVerificationResult result;
        if (envelope.Status != EnvelopeStatus.Completed || envelope.FinalDocumentId is null || envelope.FinalSha256Hash is null)
        {
            result = new EnvelopeVerificationResult(
                envelope.Id, envelope.Title, envelope.Status, envelope.CompletedAtUtc,
                IsIntact: false, DocumentHashMatches: false, CertificateHashMatches: false,
                auditIntegrity.IsIntact, auditIntegrity.RecordCount,
                envelope.OriginalSha256Hash, envelope.FinalSha256Hash, envelope.EnvelopeHash, envelope.CertificateHash, recipients);
        }
        else
        {
            var stored = await documentsClient.DownloadAsync(envelope.FinalDocumentId.Value, cancellationToken);
            var actualHashHex = Convert.ToHexStringLower(await SHA256.HashDataAsync(stored.Content, cancellationToken));
            await stored.Content.DisposeAsync();
            var documentHashMatches = string.Equals(actualHashHex, envelope.FinalSha256Hash, StringComparison.OrdinalIgnoreCase);

            var certificateHashMatches = envelope.CertificateDocument is null || envelope.CertificateHash is null
                || string.Equals(Convert.ToHexStringLower(SHA256.HashData(envelope.CertificateDocument)), envelope.CertificateHash, StringComparison.OrdinalIgnoreCase);

            result = new EnvelopeVerificationResult(
                envelope.Id, envelope.Title, envelope.Status, envelope.CompletedAtUtc,
                IsIntact: documentHashMatches && certificateHashMatches && auditIntegrity.IsIntact, documentHashMatches, certificateHashMatches,
                auditIntegrity.IsIntact, auditIntegrity.RecordCount,
                envelope.OriginalSha256Hash, envelope.FinalSha256Hash, envelope.EnvelopeHash, envelope.CertificateHash, recipients);
        }

        await integrationEventPublisher.PublishAsync(new EnvelopeVerificationPerformedV1(
            Guid.NewGuid(), DateTime.UtcNow, envelope.Id, result.IsIntact, currentActor.IpAddress, currentActor.UserAgent),
            cancellationToken);

        return Result.Success(result);
    }
}
