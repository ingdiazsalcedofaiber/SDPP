using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Infrastructure.Protection;

/// <summary>
/// Orchestrates ProtectionPolicyResolver → WatermarkContentBuilder/StampingEngine → PermissionRestrictor
/// → IntegritySigner → NotificationSender (see docs on automatic protection, "Motor de Protección").
/// Takes a document already classified by InspectDocumentHandler — <see cref="ProtectionContext"/>
/// carries the classification/category/labels/score it needs, so this never re-runs OCR or
/// classification itself (performance requirement from the spec).
/// </summary>
public sealed class ProtectionEngine(
    IOptionsMonitor<ProtectionPolicyConfig> policyOptions,
    IWatermarkContentBuilder watermarkContentBuilder,
    IProtectionStampingEngine stampingEngine,
    IPdfPermissionRestrictor permissionRestrictor,
    IDocumentIntegritySigner integritySigner,
    INotificationSender notificationSender,
    ILogger<ProtectionEngine> logger)
    : IProtectionEngine
{
    /// <summary>The only operations that hand out an editable (non-PDF, non-image) output today —
    /// see Documents.Domain.Enums.OperationType. Kept as plain strings (not the Documents-owned
    /// enum, which this module cannot reference) because "produces an editable format" is a
    /// protection-policy concept, not a conversion-engine one; ClassificationPolicy's
    /// category-based block (Historia Clínica → these same three operations) uses the identical
    /// list independently in DefaultDataSeeder, since that block fires earlier, before a job is
    /// even queued.</summary>
    private static readonly HashSet<string> EditableOutputOperations =
        new(StringComparer.OrdinalIgnoreCase) { "PdfToWord", "PdfToExcel", "PdfToPpt" };

    public async Task<ProtectionResult> ApplyAsync(
        string inputFilePath, ProtectionContext context, CancellationToken cancellationToken = default)
    {
        var policy = policyOptions.CurrentValue.ResolveFor(context.Classification.ToString());

        if (policy.BlockEditableConversion && EditableOutputOperations.Contains(context.OperationType))
        {
            var reason =
                $"La clasificación '{context.Classification}' de este documento bloquea la conversión a formatos editables; " +
                "solo se permite como PDF protegido o visualización dentro de la plataforma.";
            logger.LogWarning(
                "Conversión bloqueada por protección automática: documento {DocumentId}, operación {OperationType}, auditId {AuditId}",
                context.DocumentId, context.OperationType, context.AuditId);
            return new ProtectionResult(inputFilePath, [], null, Blocked: true, reason);
        }

        var appliedProtections = new List<string>();
        var currentPath = inputFilePath;
        var isPdf = string.Equals(Path.GetExtension(inputFilePath), ".pdf", StringComparison.OrdinalIgnoreCase);

        if (isPdf && (policy.ApplyWatermark || policy.ApplyFooter || policy.ApplyHeader || policy.EmbedMetadata))
        {
            var watermarkText = watermarkContentBuilder.Build(policy.WatermarkTemplate, context);
            var footerText = watermarkContentBuilder.Build(policy.FooterTemplate, context);
            var headerText = watermarkContentBuilder.Build(policy.HeaderTemplate, context);

            var stampedPath = stampingEngine.Stamp(currentPath, policy, context, watermarkText, footerText, headerText);
            TryDelete(currentPath, keep: inputFilePath);
            currentPath = stampedPath;

            if (policy.ApplyWatermark) appliedProtections.Add("Watermark");
            if (policy.ApplyFooter) appliedProtections.Add("Footer");
            if (policy.ApplyHeader) appliedProtections.Add("Header");
            if (policy.EmbedMetadata) appliedProtections.Add("Metadata");
        }

        if (isPdf && policy.BlockPrintAndCopy)
        {
            var restrictedPath = await permissionRestrictor.RestrictPrintAndCopyAsync(currentPath, cancellationToken);
            TryDelete(currentPath, keep: inputFilePath);
            currentPath = restrictedPath;
            appliedProtections.Add("BlockPrintAndCopy");
        }

        string? signature = null;
        if (policy.SignIntegrity)
        {
            var bytes = await File.ReadAllBytesAsync(currentPath, cancellationToken);
            signature = integritySigner.Sign(bytes);
            appliedProtections.Add("IntegritySignature");
        }

        if (policy.NotifyAdmin)
        {
            await notificationSender.NotifyAsync(
                new NotificationMessage(
                    context.DocumentId, context.AuditId,
                    Subject: $"Documento {context.Classification} protegido automáticamente",
                    Body:
                        $"El documento {context.DocumentId} (categoría: {context.Category ?? "N/A"}, puntuación de riesgo: {context.RiskScore}) " +
                        $"fue procesado con la operación '{context.OperationType}' y protegido automáticamente. Auditoría: {context.AuditId}.",
                    Metadata: new Dictionary<string, string>
                    {
                        ["classification"] = context.Classification.ToString(),
                        ["category"] = context.Category ?? string.Empty,
                        ["riskScore"] = context.RiskScore.ToString(),
                        ["operationType"] = context.OperationType,
                    }),
                cancellationToken);
            appliedProtections.Add("AdminNotification");
        }

        return new ProtectionResult(currentPath, appliedProtections, signature, Blocked: false, null);
    }

    private static void TryDelete(string path, string keep)
    {
        if (string.Equals(path, keep, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort cleanup */ }
    }
}
