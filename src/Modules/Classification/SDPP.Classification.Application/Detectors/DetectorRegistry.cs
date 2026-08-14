using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.Detectors;

/// <summary>Resolves the IDetector strategy for a given DlpRule.DetectorType (docs/05-security/dlp-engine.md §2).</summary>
public sealed class DetectorRegistry(IEnumerable<IDetector> detectors)
{
    private readonly Dictionary<DetectorType, IDetector> _byType = detectors.ToDictionary(d => d.SupportedType);

    public IDetector Resolve(DetectorType type) =>
        _byType.TryGetValue(type, out var detector)
            ? detector
            : throw new NotSupportedException($"No hay un detector registrado para el tipo '{type}'.");
}
