using SDPP.Classification.Application.Detectors;

namespace SDPP.Classification.Application;

public sealed record AiClassificationSuggestion(string? BusinessCategory, IReadOnlyList<string> Labels, double Confidence);

/// <summary>
/// Extension point for a future ML/LLM-backed classification stage (see the "AIClassifier" box in
/// the platform's DLP pipeline diagram) — deliberately not implemented against any model today:
/// this project never calls out to a cloud service, and no on-prem model is provisioned yet. The
/// rule engine (DlpRule/IDetector/RiskScoringEngine/LabelEngine) remains the sole source of truth
/// for classification. Wired into InspectDocumentHandler with <see cref="NoOpAiClassifier"/> as
/// the only implementation, so a real one can be dropped in later via DI without touching the
/// pipeline — see docs/05-security/classification-engine.md.
/// </summary>
public interface IAiClassifier
{
    Task<AiClassificationSuggestion?> ClassifyAsync(DetectionContext context, CancellationToken cancellationToken = default);
}

public sealed class NoOpAiClassifier : IAiClassifier
{
    public Task<AiClassificationSuggestion?> ClassifyAsync(DetectionContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult<AiClassificationSuggestion?>(null);
}
