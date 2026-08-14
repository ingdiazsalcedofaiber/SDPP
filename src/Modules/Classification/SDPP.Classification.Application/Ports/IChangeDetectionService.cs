using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Ports;

public sealed record ChangeDetectionInput(
    string? NewPhysicalHash, string NewContentType, string NewContentFingerprint, string NewStructuralSignature,
    DocumentVersionFingerprint? PreviousVersion, string? PreviousPhysicalHash, string? PreviousContentType);

/// <summary>
/// Pure decision logic ("¿esto es una conversión de formato, un cambio de metadatos, o una
/// modificación real?") — see the integrity-and-fingerprint proposal, "Motor de detección de
/// cambios". Moved here from the Documents module as part of the "Clasificación de Activos de
/// Información" extraction. Deliberately has no infrastructure dependency (no I/O), so it's
/// trivially unit-testable and safe to call synchronously from ClassifyDocumentHandler.
/// <see cref="ChangeDetectionInput.NewPhysicalHash"/> is nullable because the calling document's
/// own DocumentIntegrityRecord may not have been registered yet by the async
/// DocumentUploadedV1/ConversionCompletedV1 consumer at the moment classification runs — the
/// comparison degrades gracefully to the fingerprint/structural-signature signals in that case.
/// </summary>
public interface IChangeDetectionService
{
    ChangeType Detect(ChangeDetectionInput input);
}
