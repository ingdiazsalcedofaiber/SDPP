using SDPP.Classification.Application.Ports;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Services;

/// <summary>See the integrity-and-fingerprint proposal, "Motor de detección de cambios" — the
/// decision tree that decides whether ClassifyDocumentHandler needs to run a real inspection at
/// all.</summary>
public sealed class ChangeDetectionService(IContentFingerprintService fingerprintService) : IChangeDetectionService
{
    /// <summary>Hamming distance (out of 64 bits) below which two SimHash signatures are treated
    /// as extraction noise from the same underlying content, not a real edit.</summary>
    private const int NearDuplicateThreshold = 3;

    /// <summary>Above NearDuplicateThreshold and up to this value: a real but limited edit
    /// (PartialModification). Beyond it: TotalModification.</summary>
    private const int PartialModificationThreshold = 12;

    public ChangeType Detect(ChangeDetectionInput input)
    {
        if (input.PreviousVersion is null || input.PreviousVersion.ContentFingerprint is null)
        {
            return ChangeType.Initial;
        }

        if (input.PreviousPhysicalHash is not null && input.NewPhysicalHash == input.PreviousPhysicalHash)
        {
            return ChangeType.None;
        }

        if (input.NewContentFingerprint == input.PreviousVersion.ContentFingerprint)
        {
            return input.NewContentType == input.PreviousContentType
                ? ChangeType.MetadataOnly
                : ChangeType.FormatConversion;
        }

        if (input.PreviousVersion.StructuralSignature is null)
        {
            // No structural signature to compare against (shouldn't normally happen once
            // ContentFingerprint is set, but fail toward the safer "reclassify" branch).
            return ChangeType.TotalModification;
        }

        var distance = fingerprintService.HammingDistance(input.NewStructuralSignature, input.PreviousVersion.StructuralSignature);

        return distance switch
        {
            <= NearDuplicateThreshold => ChangeType.FormatConversionNoise,
            <= PartialModificationThreshold => ChangeType.PartialModification,
            _ => ChangeType.TotalModification,
        };
    }
}
