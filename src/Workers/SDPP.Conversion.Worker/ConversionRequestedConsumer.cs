using System.Diagnostics;
using System.Security.Cryptography;
using MassTransit;
using Microsoft.Extensions.Logging;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Classification.Application.Ports;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;
using SDPP.Documents.Domain.ValueObjects;
using DocumentInstance = SDPP.Documents.Domain.Aggregates.DocumentInstance;

namespace SDPP.Conversion.Worker;

/// <summary>
/// Consumes ConversionRequestedV1 (published by SDPP.Documents.Application.RequestConversion
/// once the policy engine returns Allow) and runs it through the matching IConversionEngine —
/// see docs/01-architecture/c4-diagrams.md, "Conversion Worker Pool". Reuses the Documents
/// module's own repository/aggregate so every state transition still goes through
/// ConversionJob's invariants instead of writing to the database directly. The watermark/protection
/// stack (IProtectionEngine) now lives in the Classification module — see the "Clasificación de
/// Activos de Información" extraction — referenced here in-process (never over HTTP) since it
/// manipulates the same local output file this consumer already has on disk.
/// </summary>
public sealed class ConversionRequestedConsumer(
    IDocumentRepository repository,
    IBlobStorage blobStorage,
    IEnumerable<IConversionEngine> engines,
    IProtectionEngine protectionEngine,
    IUnitOfWork unitOfWork,
    IIntegrationEventPublisher integrationEventPublisher,
    ILogger<ConversionRequestedConsumer> logger)
    : IConsumer<ConversionRequestedV1>
{
    public async Task Consume(ConsumeContext<ConversionRequestedV1> context)
    {
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        var document = await repository.GetByIdAsync(message.DocumentId, cancellationToken);
        if (document is null)
        {
            logger.LogError("ConversionRequestedV1 recibido para un documento inexistente {DocumentId}", message.DocumentId);
            return;
        }

        var operationType = Enum.Parse<OperationType>(message.OperationType);
        document.StartProcessingJob(message.JobId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var inputPaths = new List<string>();

        try
        {
            var engine = engines.FirstOrDefault(e => e.CanHandle(operationType))
                ?? throw new NotSupportedException($"No hay un motor de conversión registrado para '{operationType}'.");

            inputPaths.Add(await DownloadToTempFileAsync(
                new StoragePath(message.StorageBucket, message.StorageObjectKey), cancellationToken));

            // Merge is the one operation with more than one input — everything else ignores this.
            if (message.OperationParameters.TryGetValue("additionalDocumentIds", out var additionalIdsRaw))
            {
                foreach (var idText in additionalIdsRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!Guid.TryParse(idText, out var additionalId))
                    {
                        throw new InvalidOperationException($"'{idText}' en additionalDocumentIds no es un identificador válido.");
                    }

                    var additionalDocument = await repository.GetByIdAsync(additionalId, cancellationToken)
                        ?? throw new InvalidOperationException($"El documento adicional {additionalId} no existe.");

                    inputPaths.Add(await DownloadToTempFileAsync(additionalDocument.StorageLocation, cancellationToken));
                }
            }

            var engineResult = await engine.ConvertAsync(inputPaths, operationType, message.OperationParameters, cancellationToken);

            if (!engineResult.Success)
            {
                stopwatch.Stop();
                await FailAsync(document, message, engineResult.ErrorDetail ?? "Error desconocido del motor de conversión.", cancellationToken);
                return;
            }

            // The document is already classified (message carries the same RiskScore/Category/Labels
            // Classification.Api computed) — ProtectionEngine never re-inspects or re-runs OCR,
            // it only stamps the already-produced output before it's handed out (see docs on
            // protección automática, "aplicar antes de la entrega").
            var auditId = Guid.NewGuid();
            var protectionContext = new ProtectionContext(
                document.Id, message.JobId, auditId,
                Enum.Parse<SDPP.Classification.Domain.Enums.ClassificationLevel>(message.DeclaredClassification),
                message.Category, message.Labels ?? [], message.RiskScore, operationType.ToString(), message.Area, message.Actor);
            var protectionResult = await protectionEngine.ApplyAsync(
                engineResult.OutputFilePath!, protectionContext, cancellationToken);
            stopwatch.Stop();

            if (protectionResult.Blocked)
            {
                TryDelete(engineResult.OutputFilePath!);
                await BlockAsync(document, message, protectionResult.BlockReason!, auditId, cancellationToken);
                return;
            }

            var (outputDocument, outputHashHex) = await UploadOutputAsync(
                document, protectionResult.OutputFilePath, cancellationToken);

            document.CompleteJob(message.JobId, outputDocument.Id, engineResult.EngineUsed, (int)stopwatch.ElapsedMilliseconds);
            repository.Add(outputDocument);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            await integrationEventPublisher.PublishAsync(new ConversionCompletedV1(
                Guid.NewGuid(), DateTime.UtcNow, message.JobId, document.Id, outputDocument.Id,
                outputHashHex, engineResult.EngineUsed, (int)stopwatch.ElapsedMilliseconds,
                message.DeclaredClassification, outputDocument.DocumentVersionId, outputDocument.ContentType), cancellationToken);

            if (protectionResult.ProtectionsApplied.Count > 0)
            {
                await integrationEventPublisher.PublishAsync(new ProtectionAppliedV1(
                    Guid.NewGuid(), DateTime.UtcNow, auditId, document.Id, message.JobId, outputDocument.Id,
                    message.DeclaredClassification, message.Category, message.Labels ?? [], message.RiskScore,
                    protectionResult.ProtectionsApplied, outputHashHex, protectionResult.IntegritySignature,
                    outputDocument.DocumentVersionId, outputDocument.ContentType),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo al procesar el job de conversión {JobId}", message.JobId);
            await FailAsync(document, message, ex.Message, cancellationToken);
        }
        finally
        {
            foreach (var path in inputPaths)
            {
                TryDelete(path);
            }
        }
    }

    private async Task<string> DownloadToTempFileAsync(StoragePath storagePath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdpp-worker-in-{Guid.NewGuid():N}");
        await using var inputStream = await blobStorage.OpenReadAsync(storagePath, cancellationToken);
        await using var inputFile = new FileStream(path, FileMode.Create, FileAccess.Write);
        await inputStream.CopyToAsync(inputFile, cancellationToken);
        return path;
    }

    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".zip"] = "application/zip",
    };

    /// <summary>Uploads a conversion output and returns it together with its freshly computed
    /// SHA-256 hex — the hash is computed here, inline, over bytes already on disk (see the
    /// "Clasificación de Activos de Información" extraction: Classification owns hash storage,
    /// not necessarily its computation) and only ever leaves this method via the
    /// ConversionCompletedV1/ProtectionAppliedV1 events published by the caller, never stored on
    /// the DocumentInstance aggregate itself anymore.</summary>
    private async Task<(DocumentInstance Document, string HashHex)> UploadOutputAsync(
        DocumentInstance sourceDocument, string outputFilePath, CancellationToken cancellationToken)
    {
        await using var outputStream = File.OpenRead(outputFilePath);
        var hashHex = Convert.ToHexStringLower(await SHA256.HashDataAsync(outputStream, cancellationToken));
        outputStream.Position = 0;

        var outputExtension = Path.GetExtension(outputFilePath);
        // The temp file's own name (a GUID with no meaningful stem) is never what a user should
        // see in a download dialog — base it on the source document's name instead.
        var outputFileName = Path.GetFileNameWithoutExtension(sourceDocument.OriginalFileName) + outputExtension;
        var contentType = ContentTypesByExtension.GetValueOrDefault(outputExtension, "application/octet-stream");

        // A worker-driven conversion is known lineage with certainty — it attaches to the SAME
        // DocumentVersion as its source (never a new one, so it's never reclassified) and records
        // ConvertedFromInstanceId explicitly.
        var outputDocument = DocumentInstance.Upload(
            sourceDocument.OwnerId, outputFileName, contentType, outputStream.Length,
            sourceDocument.DocumentVersionId, sourceDocument.Id);

        outputDocument.CompleteInspection(requiresManualReview: false);

        outputStream.Position = 0;
        await blobStorage.SaveAsync(outputDocument.StorageLocation, outputStream, contentType, cancellationToken);

        TryDelete(outputFilePath);
        return (outputDocument, hashHex);
    }

    private async Task FailAsync(DocumentInstance document, ConversionRequestedV1 message, string errorDetail, CancellationToken cancellationToken)
    {
        document.FailJob(message.JobId, errorDetail);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(
            new ConversionFailedV1(Guid.NewGuid(), DateTime.UtcNow, message.JobId, document.Id, "ENGINE_ERROR", errorDetail),
            cancellationToken);
    }

    /// <summary>Reached only when ProtectionEngine's blockEditableConversion check fires (Secreta-level
    /// documents requesting an editable output) — the category-based block (e.g. Historia Clínica)
    /// already happens earlier, in RequestConversionHandler, before a job is ever queued.</summary>
    private async Task BlockAsync(
        DocumentInstance document, ConversionRequestedV1 message, string reason, Guid auditId, CancellationToken cancellationToken)
    {
        document.FailJob(message.JobId, reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await integrationEventPublisher.PublishAsync(
            new ConversionFailedV1(Guid.NewGuid(), DateTime.UtcNow, message.JobId, document.Id, "PROTECTION_BLOCKED", reason),
            cancellationToken);
        await integrationEventPublisher.PublishAsync(
            new ConversionBlockedV1(
                Guid.NewGuid(), DateTime.UtcNow, document.Id, message.JobId, message.OperationType,
                message.DeclaredClassification, message.Category, reason, message.Actor),
            cancellationToken);
        logger.LogWarning(
            "Job {JobId} bloqueado por protección automática (auditId {AuditId}): {Reason}", message.JobId, auditId, reason);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort cleanup */ }
    }
}
