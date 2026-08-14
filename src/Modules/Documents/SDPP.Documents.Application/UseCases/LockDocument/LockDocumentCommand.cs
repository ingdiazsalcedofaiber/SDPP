using SDPP.BuildingBlocks.Application;

namespace SDPP.Documents.Application.UseCases.LockDocument;

public sealed record LockDocumentResult(Guid DocumentId);

public sealed record LockDocumentCommand(Guid DocumentId) : ICommand<LockDocumentResult>;
