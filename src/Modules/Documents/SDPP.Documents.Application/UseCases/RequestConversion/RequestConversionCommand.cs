using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Application.UseCases.RequestConversion;

public sealed record RequestConversionResult(Guid JobId, string Status);

/// <summary>
/// The Conversion Panel's only request: convert this document. No mandatory business form —
/// see the Panel de Conversión simplification (SRP: this module only converts files).
/// </summary>
public sealed record RequestConversionCommand(
    Guid DocumentId,
    OperationType OperationType,
    IReadOnlyDictionary<string, string> OperationParameters) : ICommand<RequestConversionResult>;
