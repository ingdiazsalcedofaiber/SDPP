using System.IO.Compression;
using System.Text.Json;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Application.UseCases.VerifyEnvelope;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.ExportEvidence;

public sealed record EvidencePackageFile(byte[] Content, string FileName);

/// <summary>
/// Backs GET /envelopes/{id}/evidence-package — a downloadable ZIP with everything a party needs to
/// independently substantiate a completed envelope: the signed document, the standalone certificate,
/// the real audit trail (fetched from Audit.Api, not duplicated locally), the cryptographic
/// signature records, a consolidated evidence summary (including the consent declarations actually
/// shown to each signer), the same integrity/audit-chain verification the public verifier performs,
/// and envelope metadata. Same authorization as the certificate download (creator/admin or one of
/// the envelope's own recipients) — this is a private artifact, unlike the public /verify endpoint.
/// </summary>
public sealed record ExportEvidencePackageQuery(Guid EnvelopeId) : IQuery<EvidencePackageFile>;

public sealed class ExportEvidencePackageHandler(
    ISignatureEnvelopeRepository repository, IDocumentsClient documentsClient, IAuditClient auditClient,
    ISender sender, ICurrentActor currentActor, IOrganizationContextProvider organizationContextProvider)
    : IRequestHandler<ExportEvidencePackageQuery, Result<EvidencePackageFile>>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<Result<EvidencePackageFile>> Handle(ExportEvidencePackageQuery request, CancellationToken cancellationToken)
    {
        var envelope = await repository.GetByIdAsync(request.EnvelopeId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<EvidencePackageFile>("El sobre no existe.", "ENVELOPE_NOT_FOUND");
        }

        var isRecipient = envelope.Recipients.Any(r => r.MatchedUserId == currentActor.UserId);
        if (!EnvelopeAuthorization.CanManage(envelope, currentActor, organizationContextProvider.GetCurrentOrganizationId()) && !isRecipient)
        {
            return Result.Failure<EvidencePackageFile>("No tienes permiso para ver este sobre.", "FORBIDDEN");
        }

        if (envelope.Status != EnvelopeStatus.Completed || envelope.FinalDocumentId is not { } finalDocumentId)
        {
            return Result.Failure<EvidencePackageFile>("El paquete de evidencia solo está disponible para sobres completados.", "NOT_COMPLETED");
        }

        var verification = await sender.Send(new VerifyEnvelopeQuery(envelope.Id), cancellationToken);

        var subjectIds = new[] { envelope.Id, envelope.SourceDocumentId, envelope.FinalDocumentId ?? Guid.Empty }
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var auditRecords = await auditClient.GetRecordsAsync(subjectIds, cancellationToken);

        var finalDocument = await documentsClient.DownloadAsync(finalDocumentId, cancellationToken);
        byte[] finalDocumentBytes;
        await using (finalDocument.Content)
        {
            using var buffer = new MemoryStream();
            await finalDocument.Content.CopyToAsync(buffer, cancellationToken);
            finalDocumentBytes = buffer.ToArray();
        }

        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            await AddEntryAsync(zip, "document.pdf", finalDocumentBytes, cancellationToken);
            if (envelope.CertificateDocument is not null)
            {
                await AddEntryAsync(zip, "certificate.pdf", envelope.CertificateDocument, cancellationToken);
            }

            await AddJsonEntryAsync(zip, "audit-trail.json", auditRecords, cancellationToken);

            await AddJsonEntryAsync(zip, "signatures.json", envelope.DocumentSignatures.Select(s => new
            {
                s.Id,
                s.RecipientId,
                s.DocumentId,
                s.DocumentVersionId,
                s.DocumentHashAtSigning,
                s.CanonicalPayloadHash,
                s.CryptographicSignatureBase64,
                s.PublicKeyId,
                s.Algorithm,
                s.ConsentId,
                s.TimestampUtc,
                s.TimestampSource,
            }), cancellationToken);

            await AddJsonEntryAsync(zip, "verification.json", verification.Value, cancellationToken);

            await AddJsonEntryAsync(zip, "evidence.json", new
            {
                envelope.Id,
                envelope.Title,
                envelope.CreatedAtUtc,
                envelope.SentAtUtc,
                envelope.CompletedAtUtc,
                envelope.OriginalSha256Hash,
                envelope.FinalSha256Hash,
                envelope.EnvelopeHash,
                envelope.CertificateHash,
                Recipients = envelope.Recipients.Select(r => new
                {
                    r.Id, r.Email, r.FullName, r.Order, Status = r.Status.ToString(), r.AuthMethodUsed,
                    r.SentAtUtc, r.ViewedAtUtc, r.SignedAtUtc, r.ViewedIpAddress, r.SignedIpAddress,
                }),
                Consents = envelope.ConsentRecords.Select(c => new
                {
                    c.Id, c.RecipientId, c.ConsentText, c.ConsentVersion, c.TimestampUtc, c.IpAddress, c.AuthenticationMethod,
                }),
            }, cancellationToken);

            await AddJsonEntryAsync(zip, "metadata.json", new
            {
                envelope.Id,
                envelope.SourceDocumentId,
                envelope.SourceDocumentVersionId,
                envelope.FinalDocumentId,
                envelope.FinalDocumentVersionId,
                SigningMode = envelope.SigningMode.ToString(),
                envelope.DueDateUtc,
                GeneratedAtUtc = DateTime.UtcNow,
                LegalNotice =
                    "Este paquete de evidencia documenta un proceso de firma electrónica con trazabilidad: " +
                    "identificación, autenticación, consentimiento, integridad (SHA-256) y auditoría encadenada. " +
                    "No constituye una firma digital certificada basada en PKI/X.509 ni una certificación emitida " +
                    "por una entidad de certificación. Su validez jurídica debe evaluarse conforme a la " +
                    "legislación aplicable y las circunstancias del caso.",
            }, cancellationToken);
        }

        return Result.Success(new EvidencePackageFile(zipStream.ToArray(), $"evidencia-{envelope.Id}.zip"));
    }

    private static async Task AddEntryAsync(ZipArchive zip, string name, byte[] content, CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(content, cancellationToken);
    }

    private static Task AddJsonEntryAsync<T>(ZipArchive zip, string name, T data, CancellationToken cancellationToken) =>
        AddEntryAsync(zip, name, JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions), cancellationToken);
}
