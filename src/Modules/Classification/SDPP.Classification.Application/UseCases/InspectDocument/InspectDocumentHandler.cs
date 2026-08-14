using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Classification.Application.Detectors;
using SDPP.Classification.Application.Ports;
using SDPP.Classification.Domain.Aggregates;

namespace SDPP.Classification.Application.UseCases.InspectDocument;

/// <summary>
/// Implements the inspection pipeline of docs/05-security/classification-engine.md §3: fetch
/// extracted text/metadata, run every enabled DlpRule through its matching detector, then run the
/// findings through RiskScoringEngine/LabelEngine (and IAiClassifier, currently a no-op — see
/// IAiClassifier.cs) before handing everything to InspectionResult.Complete. Runs synchronously
/// because Document API calls this endpoint and blocks on the result before letting a conversion
/// proceed ("fail closed" — docs/00-overview.md §4.2 — any unhandled exception here must surface
/// as an error, not a silently empty result).
/// </summary>
public sealed class InspectDocumentHandler(
    IDocumentContentClient contentClient,
    IDlpRuleRepository ruleRepository,
    IInspectionResultRepository inspectionRepository,
    DetectorRegistry detectorRegistry,
    IRiskScoringEngine riskScoringEngine,
    ILabelEngine labelEngine,
    IAiClassifier aiClassifier,
    IUnitOfWork unitOfWork)
    : IRequestHandler<InspectDocumentCommand, Result<InspectDocumentResult>>
{
    public async Task<Result<InspectDocumentResult>> Handle(InspectDocumentCommand request, CancellationToken cancellationToken)
    {
        var content = await contentClient.GetContentForInspectionAsync(request.DocumentId, cancellationToken);
        var rules = await ruleRepository.GetEnabledRulesAsync(cancellationToken);

        var context = new DetectionContext(content.FileName, content.ExtractedText, content.Metadata);

        var findings = rules
            .SelectMany(rule => detectorRegistry.Resolve(rule.DetectorType).Detect(context, rule))
            .ToList();

        var riskScore = riskScoringEngine.Score(findings);
        var labeling = labelEngine.Resolve(findings);

        // The AI stage never overrides the rule engine's category — it can only *add* labels,
        // since it has no vote on classification level today (see IAiClassifier.cs).
        var aiSuggestion = await aiClassifier.ClassifyAsync(context, cancellationToken);
        var labels = aiSuggestion is null
            ? labeling.Labels
            : labeling.Labels.Union(aiSuggestion.Labels, StringComparer.OrdinalIgnoreCase).ToList();
        var businessCategory = labeling.BusinessCategory ?? aiSuggestion?.BusinessCategory;

        var inspection = InspectionResult.Start(request.DocumentId, request.Trigger);
        inspection.Complete(findings, riskScore, labels, businessCategory);

        inspectionRepository.Add(inspection);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var findingDtos = inspection.Findings
            .Select(f => new FindingDto(f.DetectorId, f.Category.ToString(), f.Severity.ToString(), f.MatchCount, f.Location))
            .ToList();

        return Result.Success(new InspectDocumentResult(
            inspection.Id, inspection.SuggestedClassification.ToString(), inspection.RequiresManualReview, findingDtos,
            inspection.RiskScore, inspection.Labels, inspection.BusinessCategory));
    }
}
