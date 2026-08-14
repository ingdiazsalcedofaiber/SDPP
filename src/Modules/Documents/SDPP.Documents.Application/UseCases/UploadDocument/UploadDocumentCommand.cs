using SDPP.BuildingBlocks.Application;

namespace SDPP.Documents.Application.UseCases.UploadDocument;

public sealed record UploadDocumentResult(Guid DocumentId, string Sha256Hash, string Status);

/// <summary>
/// Content is passed as a stream rather than a byte[] to avoid buffering large files in memory
/// (mitigates part of docs/05-security/threat-model-stride.md D1/D2). OwnerId deliberately comes
/// from ICurrentActor inside the handler, never from this payload — a client cannot upload on
/// someone else's behalf by tampering with the request body.
/// </summary>
public sealed record UploadDocumentCommand(
    Stream Content,
    string OriginalFileName,
    string DeclaredContentType,
    long SizeBytes) : ICommand<UploadDocumentResult>;
