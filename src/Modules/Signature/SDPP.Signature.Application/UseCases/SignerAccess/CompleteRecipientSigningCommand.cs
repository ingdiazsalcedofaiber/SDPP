using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Domain.Aggregates;
using SDPP.Signature.Domain.Enums;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

public sealed record FieldSubmission(Guid FieldId, string? Value, byte[]? SignatureImage, SignatureMethodUsed? Method);

public sealed record CompleteRecipientSigningResult(string EnvelopeStatus, bool EnvelopeCompleted, Guid? FinalDocumentId);

/// <summary>Everything this recipient needs to sign arrives in one call — nothing is persisted
/// per-field before this (see EnvelopeField.Fill, only reachable through
/// SignatureEnvelope.RegisterSignature), matching how DocuSign-style flows actually work: the
/// recipient fills fields client-side and nothing touches the server until "Finish".</summary>
public sealed record CompleteRecipientSigningCommand(string RawToken, string? SessionToken, IReadOnlyList<FieldSubmission> Fields)
    : ICommand<CompleteRecipientSigningResult>;

/// <summary>Marks this recipient Signed and, if they were the last one, assembles the final PDF
/// (every recipient's fields embedded in one pass), appends the audit certificate page, uploads it
/// as a new locked DocumentVersion, and completes the envelope — all in one handler so the envelope
/// never sits in an inconsistent "everyone signed but no final document" state across requests.</summary>
public sealed class CompleteRecipientSigningHandler(
    ISignatureEnvelopeRepository envelopeRepository,
    ISignerAccessChallengeRepository challengeRepository,
    IDocumentsClient documentsClient,
    IPdfEnvelopeEmbeddingEngine embeddingEngine,
    IPublicWebLinkBuilder publicWebLinkBuilder,
    IKeyManagementService keyManagementService,
    ITimestampAuthorityService timestampAuthorityService,
    IUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IIntegrationEventPublisher integrationEventPublisher,
    INotificationRepository notificationRepository,
    ILegalApprovalStampPolicy legalApprovalStampPolicy,
    IEmailSender emailSender)
    : IRequestHandler<CompleteRecipientSigningCommand, Result<CompleteRecipientSigningResult>>
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(30);

    public async Task<Result<CompleteRecipientSigningResult>> Handle(CompleteRecipientSigningCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawToken)));
        var challenge = await challengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null || !challenge.IsLinkUsable)
        {
            return Result.Failure<CompleteRecipientSigningResult>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var envelope = await envelopeRepository.GetByRecipientIdAsync(challenge.RecipientId, cancellationToken);
        var recipient = envelope?.Recipients.FirstOrDefault(r => r.Id == challenge.RecipientId);
        if (envelope is null || recipient is null)
        {
            return Result.Failure<CompleteRecipientSigningResult>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }
        if (!RecipientAccessAuthorization.CanAct(recipient, challenge, currentActor, request.SessionToken))
        {
            return Result.Failure<CompleteRecipientSigningResult>("No estás autenticado para firmar como este destinatario.", "FORBIDDEN");
        }

        // Fail-closed: even if a LegalApprovalStamp field was somehow assigned to (or its
        // submission spoofed for) the wrong recipient, the actual fill is rejected here — the one
        // point that can never be bypassed by calling the API directly. See
        // FieldType.LegalApprovalStamp's doc comment.
        var submittedFieldIds = request.Fields.Select(f => f.FieldId).ToHashSet();
        var hasUnauthorizedLegalStamp = envelope.Fields.Any(f =>
            f.Type == FieldType.LegalApprovalStamp && submittedFieldIds.Contains(f.Id) && !legalApprovalStampPolicy.IsAuthorized(recipient.Email));
        if (hasUnauthorizedLegalStamp)
        {
            return Result.Failure<CompleteRecipientSigningResult>(
                "Solo Gerencia Legal puede generar el sello de aprobación.", "FORBIDDEN");
        }

        var originalHashBeforeThisSignature = envelope.OriginalSha256Hash;
        var authMethod = recipient.MatchedUserId is not null ? "SdppSession" : "EmailOtp";

        EnvelopeRecipient? next;
        try
        {
            next = envelope.RegisterSignature(
                recipient.Id, request.Fields.Select(f => (f.FieldId, f.Value, f.SignatureImage, f.Method)).ToList(),
                currentActor.IpAddress, currentActor.UserAgent, authMethod);
        }
        catch (SDPP.BuildingBlocks.Domain.DomainException ex)
        {
            return Result.Failure<CompleteRecipientSigningResult>(ex.Message, "INVALID_STATE");
        }

        var fieldsForThisRecipient = envelope.Fields.Where(f => f.RecipientId == recipient.Id).ToList();
        var signatureMethodsUsed = fieldsForThisRecipient
            .Where(f => f.SignatureMethod is not null)
            .Select(f => f.SignatureMethod!.Value.ToString())
            .Distinct()
            .ToList();

        // SDPP's own platform attestation of this recipient's just-completed signature — protects
        // the recorded evidence from tampering. NOT a personal PKI signature issued to the
        // recipient (see DocumentSignature's doc comment); it signs the recipient's own field
        // hashes, not the final multi-signer PDF, which doesn't exist yet.
        // DateTime.SpecifyKind is required here: EF Core materializes SqlServer datetime2 columns
        // with Kind=Unspecified, so without this, ToString("O") silently omits the UTC offset/'Z'
        // depending on whether the recipient was just loaded from the DB or is still the same
        // in-memory instance RegisterConsent set earlier in the SAME request — an ambiguity with no
        // security impact (the hash/signature stay unique either way) but worth pinning down so the
        // canonical payload format is deterministic and independently reproducible.
        var consentTimestampUtc = recipient.ConsentAcceptedAtUtc is { } consentAt
            ? DateTime.SpecifyKind(consentAt, DateTimeKind.Utc).ToString("O")
            : string.Empty;
        var canonicalPayload = string.Join('|',
            envelope.Id, recipient.Id, recipient.Email, envelope.SourceDocumentId, envelope.SourceDocumentVersionId,
            envelope.OriginalSha256Hash, string.Join(',', fieldsForThisRecipient.OrderBy(f => f.Id).Select(f => f.SignatureHash)),
            consentTimestampUtc);
        var canonicalPayloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var canonicalPayloadHash = Convert.ToHexStringLower(SHA256.HashData(canonicalPayloadBytes));
        var cryptoResult = await keyManagementService.SignAsync(canonicalPayloadBytes, cancellationToken);
        var signatureTimestamp = timestampAuthorityService.GetTimestamp();
        var consentRecordId = envelope.ConsentRecords
            .Where(c => c.RecipientId == recipient.Id)
            .OrderByDescending(c => c.TimestampUtc)
            .FirstOrDefault()?.Id;
        envelope.RecordCryptographicSignature(
            recipient.Id, envelope.SourceDocumentId, envelope.SourceDocumentVersionId, envelope.OriginalSha256Hash,
            canonicalPayloadHash, cryptoResult.SignatureBase64, cryptoResult.KeyId, cryptoResult.Algorithm,
            consentRecordId, signatureTimestamp.TimestampUtc, signatureTimestamp.Source);

        Guid? finalDocumentId = null;
        var allSigned = envelope.Recipients.All(r => r.Status == RecipientStatus.Signed);

        if (allSigned)
        {
            finalDocumentId = await AssembleAndCompleteAsync(envelope, cancellationToken);
        }
        else if (next is not null)
        {
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var nextTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            challengeRepository.Add(SignerAccessChallenge.Issue(next.Id, nextTokenHash, LinkLifetime));
            // Phase 3 TODO: email `rawToken`'s link to `next.Email` — for now it's only retrievable
            // via the envelope detail query (creator-visible "copy link" fallback, same as SendEnvelope).
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(new EnvelopeRecipientSignedV1(
            Guid.NewGuid(), DateTime.UtcNow, envelope.Id, recipient.Id, recipient.Email, recipient.FullName,
            recipient.MatchedUserId, authMethod, string.Join(",", signatureMethodsUsed),
            originalHashBeforeThisSignature, envelope.FinalSha256Hash,
            currentActor.IpAddress, currentActor.UserAgent),
            cancellationToken);

        return Result.Success(new CompleteRecipientSigningResult(envelope.Status.ToString(), allSigned, finalDocumentId));
    }

    private async Task<Guid> AssembleAndCompleteAsync(SignatureEnvelope envelope, CancellationToken cancellationToken)
    {
        var original = await documentsClient.DownloadAsync(envelope.SourceDocumentId, cancellationToken);
        var inputPath = Path.Combine(Path.GetTempPath(), $"sdpp-envelope-in-{Guid.NewGuid():N}.pdf");
        string? embeddedPath = null;
        string? certificatePath = null;
        try
        {
            await using (var inputFile = new FileStream(inputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await original.Content.CopyToAsync(inputFile, cancellationToken);
            }
            await original.Content.DisposeAsync();

            var recipientNamesById = envelope.Recipients.ToDictionary(r => r.Id, r => r.FullName);
            var resolvedFields = envelope.Fields
                .Select(f => new ResolvedField(
                    f.Type, f.PageNumber, f.PositionX, f.PositionY, f.Width, f.Height, f.Value, f.SignatureImage,
                    f.SignatureHash, f.FilledAtUtc, recipientNamesById[f.RecipientId]))
                .ToList();
            var imageTypePriority = new[] { FieldType.Signature, FieldType.Initials, FieldType.Stamp };
            var recipientSummaries = envelope.Recipients
                .OrderBy(r => r.Order)
                .Select(r =>
                {
                    var recipientFields = envelope.Fields.Where(f => f.RecipientId == r.Id).ToList();
                    // One representative FIELD per signer on the certificate — same field for both
                    // the thumbnail image and the hash printed next to it, so "Hash de firma" on the
                    // certificate always matches the short hash caption stamped on the document page
                    // under that exact image (see DrawSignatureCaption). Deliberately NOT
                    // DocumentSignature.CanonicalPayloadHash here — that value is real and still
                    // signed by the platform key (see CryptographicSignatureId/Algorithm below), but
                    // it's a hash of the field hash plus identity/consent/timestamp context, so it
                    // never equals the on-page value and printing it as "Hash de firma" only looked
                    // like the two didn't match. Falls back to any field with a hash (no image) only
                    // for the rare recipient with no Signature/Initials/Stamp field at all.
                    var representativeField = imageTypePriority
                        .Select(type => recipientFields.FirstOrDefault(f => f.Type == type && f.SignatureImage is not null))
                        .FirstOrDefault(f => f is not null)
                        ?? recipientFields.FirstOrDefault(f => f.SignatureHash is not null);
                    var cryptographicSignature = envelope.DocumentSignatures
                        .Where(s => s.RecipientId == r.Id)
                        .OrderByDescending(s => s.TimestampUtc)
                        .FirstOrDefault();
                    return new CertificateRecipientSummary(
                        r.FullName, r.Email, r.AuthMethodUsed, r.SentAtUtc, r.ViewedAtUtc, r.ViewedIpAddress, r.SignedAtUtc, r.SignedIpAddress,
                        r.InPerson,
                        representativeField?.SignatureHash, representativeField?.SignatureImage,
                        cryptographicSignature?.Id, cryptographicSignature is null ? null : "ECDSA P-256 (atestación de plataforma SDPP)");
                })
                .ToList();
            var verificationUrl = publicWebLinkBuilder.BuildVerificationUrl(envelope.Id);
            var certificate = new AuditCertificateInfo(
                envelope.Title, envelope.Id, DateTime.UtcNow, envelope.OriginalSha256Hash, envelope.PreviewEnvelopeHash(),
                verificationUrl, recipientSummaries);

            // One document session for both fields and the certificate page — see
            // PdfSharpEnvelopeEmbeddingEngine's doc comment for why these can't be two separate
            // PdfSharp open/save cycles. The certificate is ALSO derived as its own standalone file
            // (EmbedResult.CertificatePdfPath) via a page-copy-only split of the just-saved combined
            // file — safe precisely because it draws no fonts/text of its own (same doc comment).
            var embedResult = embeddingEngine.Embed(inputPath, resolvedFields, certificate);
            embeddedPath = embedResult.CombinedPdfPath;
            certificatePath = embedResult.CertificatePdfPath;

            string finalHashHex;
            SignedVersionResult uploadResult;
            await using (var finalFile = new FileStream(embeddedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous))
            {
                finalHashHex = Convert.ToHexStringLower(await SHA256.HashDataAsync(finalFile, cancellationToken));
                finalFile.Position = 0;
                uploadResult = await documentsClient.UploadSignedVersionAsync(
                    envelope.SourceDocumentId, finalFile, original.FileName, "application/pdf", envelope.CreatedByUserId, cancellationToken);
            }

            byte[]? certificateBytes = null;
            string? certificateHashHex = null;
            if (certificatePath is not null)
            {
                certificateBytes = await File.ReadAllBytesAsync(certificatePath, cancellationToken);
                certificateHashHex = Convert.ToHexStringLower(SHA256.HashData(certificateBytes));
            }

            await documentsClient.LockAsync(uploadResult.DocumentId, cancellationToken);
            envelope.CompleteWithFinalDocument(uploadResult.DocumentId, uploadResult.DocumentVersionId, finalHashHex, certificateBytes, certificateHashHex);

            notificationRepository.Add(InAppNotification.Create(
                envelope.CreatedByUserId, NotificationType.EnvelopeCompleted,
                "Sobre completado", $"Todos los firmantes completaron \"{envelope.Title}\".", envelope.Id));

            await integrationEventPublisher.PublishAsync(new SignatureEnvelopeCompletedV1(
                Guid.NewGuid(), DateTime.UtcNow, envelope.Id, envelope.SourceDocumentId, envelope.SourceDocumentVersionId,
                uploadResult.DocumentId, uploadResult.DocumentVersionId, envelope.OriginalSha256Hash, finalHashHex),
                cancellationToken);

            if (certificateHashHex is not null)
            {
                await integrationEventPublisher.PublishAsync(new CertificateGeneratedV1(
                    Guid.NewGuid(), DateTime.UtcNow, envelope.Id, certificateHashHex, verificationUrl),
                    cancellationToken);
            }

            if (certificateBytes is not null)
            {
                await EmailCertificateToRecipientsAsync(envelope, certificateBytes, verificationUrl, cancellationToken);
            }

            return uploadResult.DocumentId;
        }
        finally
        {
            TryDelete(inputPath);
            if (embeddedPath is not null) TryDelete(embeddedPath);
            if (certificatePath is not null) TryDelete(certificatePath);
        }
    }

    /// <summary>Best-effort — a delivery failure here must never fail (or roll back) an envelope
    /// completion that already succeeded and is already saved; SmtpEmailSender/LoggingEmailSender
    /// each log their own outcome per attempt (see their doc comments), so a bare try/catch here is
    /// enough for this method to not need its own logger. Sent to every recipient (in-person and
    /// remote alike) — the certificate email is how a front-desk patient with no SDPP account gets
    /// their copy at all, since they never log back into the platform afterward.</summary>
    private async Task EmailCertificateToRecipientsAsync(
        SignatureEnvelope envelope, byte[] certificateBytes, string verificationUrl, CancellationToken cancellationToken)
    {
        var attachment = new EmailAttachment($"certificado-{envelope.Id}.pdf", certificateBytes, "application/pdf");
        var body = $"""
            <p>El documento <strong>"{envelope.Title}"</strong> fue firmado por todas las partes.</p>
            <p>Adjunto encontrarás el certificado de firma electrónica.</p>
            <p>También puedes verificarlo en línea en cualquier momento: <a href="{verificationUrl}">{verificationUrl}</a></p>
            """;

        foreach (var recipient in envelope.Recipients)
        {
            try
            {
                await emailSender.SendAsync(
                    recipient.Email, $"Certificado de firma — {envelope.Title}", body, [attachment], cancellationToken);
            }
            catch
            {
                // Best-effort by design — see the doc comment above.
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort cleanup */ }
    }
}
