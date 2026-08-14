using SDPP.BuildingBlocks.Contracts.Documents;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Ports;

/// <summary>
/// Everything ProtectionEngine needs to stamp a conversion's output — the document has already
/// been classified, so this carries that result plus the job/audit identifiers instead of
/// re-deriving anything. Mirrors the "usuario, área, fecha, IP, ID de auditoría" placeholders the
/// dynamic watermark spec calls for. Moved here from the Documents module as part of the
/// "Clasificación de Activos de Información" extraction — <see cref="OperationType"/> is carried
/// as the plain string the wire events already use (Documents.Domain.Enums.OperationType is not
/// visible from this module), since the only thing ProtectionEngine does with it is a
/// string-set membership check.
/// </summary>
public sealed record ProtectionContext(
    Guid DocumentId,
    Guid JobId,
    Guid AuditId,
    ClassificationLevel Classification,
    string? Category,
    IReadOnlyList<string> Labels,
    int RiskScore,
    string OperationType,
    string? Area,
    ActorSnapshot Actor);

public sealed record ProtectionResult(
    string OutputFilePath,
    IReadOnlyList<string> ProtectionsApplied,
    string? IntegritySignature,
    bool Blocked,
    string? BlockReason);

/// <summary>
/// Orchestrates ProtectionPolicyResolver → WatermarkContentBuilder/StampingEngine → IntegritySigner
/// (see docs on automatic protection, "Motor de Protección"). Applied only to conversion outputs,
/// never to the original upload. Never re-runs OCR or classification: <see cref="ProtectionContext"/>
/// already carries everything it needs.
/// </summary>
public interface IProtectionEngine
{
    Task<ProtectionResult> ApplyAsync(string inputFilePath, ProtectionContext context, CancellationToken cancellationToken = default);
}
