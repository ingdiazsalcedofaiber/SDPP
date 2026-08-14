using SDPP.BuildingBlocks.Application;
using SDPP.Classification.Domain.Enums;

namespace SDPP.Classification.Application.UseCases.InspectDocument;

public sealed record FindingDto(string DetectorId, string Category, string Severity, int MatchCount, string Location);

public sealed record InspectDocumentResult(
    Guid InspectionId, string SuggestedClassification, bool RequiresManualReview, IReadOnlyList<FindingDto> Findings,
    int RiskScore, IReadOnlyList<string> Labels, string? BusinessCategory);

public sealed record InspectDocumentCommand(Guid DocumentId, InspectionTrigger Trigger) : ICommand<InspectDocumentResult>;
