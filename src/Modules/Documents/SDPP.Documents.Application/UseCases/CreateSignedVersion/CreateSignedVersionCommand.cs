using SDPP.BuildingBlocks.Application;

namespace SDPP.Documents.Application.UseCases.CreateSignedVersion;

public sealed record CreateSignedVersionResult(Guid DocumentId, Guid DocumentVersionId, Guid SourceDocumentVersionId, string Sha256Hash);

/// <summary>CreatedByUserId is supplied explicitly by the caller rather than read from
/// ICurrentActor — this endpoint is reachable via the internal-service-key credential (an external
/// envelope recipient with no SDPP session at all, relayed through Signature.Api), where
/// ICurrentActor.UserId has nothing to resolve and would throw. Signature.Api passes the
/// envelope's CreatedByUserId (the one real, always-known SDPP account behind any envelope).</summary>
public sealed record CreateSignedVersionCommand(
    Stream Content, string OriginalFileName, string ContentType, long SizeBytes, Guid SourceDocumentId, Guid CreatedByUserId)
    : ICommand<CreateSignedVersionResult>;
