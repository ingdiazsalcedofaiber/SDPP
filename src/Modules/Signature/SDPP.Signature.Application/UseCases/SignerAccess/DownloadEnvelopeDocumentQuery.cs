using System.Security.Cryptography;
using System.Text;
using MediatR;
using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Application.UseCases.SignerAccess;

public sealed record EnvelopeDocumentDownload(string FileName, string ContentType, Stream Content);

/// <summary>Backs GET /access/{token}/document — lets an external recipient (no SDPP session, so
/// they can't call Documents.Api's own download endpoint with a real user token) view the PDF
/// they're about to sign. Signature.Api fetches it server-side via IDocumentsClient (which attaches
/// the internal service key) and streams it back; the browser never talks to Documents.Api
/// directly. Naturally scoped: the token only ever resolves to its own envelope's SourceDocumentId.</summary>
public sealed record DownloadEnvelopeDocumentQuery(string RawToken) : IQuery<EnvelopeDocumentDownload>;

public sealed class DownloadEnvelopeDocumentHandler(
    ISignerAccessChallengeRepository challengeRepository, ISignatureEnvelopeRepository envelopeRepository, IDocumentsClient documentsClient)
    : IRequestHandler<DownloadEnvelopeDocumentQuery, Result<EnvelopeDocumentDownload>>
{
    public async Task<Result<EnvelopeDocumentDownload>> Handle(DownloadEnvelopeDocumentQuery request, CancellationToken cancellationToken)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RawToken)));
        var challenge = await challengeRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (challenge is null || !challenge.IsLinkUsable)
        {
            return Result.Failure<EnvelopeDocumentDownload>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var envelope = await envelopeRepository.GetByRecipientIdAsync(challenge.RecipientId, cancellationToken);
        if (envelope is null)
        {
            return Result.Failure<EnvelopeDocumentDownload>("El enlace no es válido o ha expirado.", "LINK_INVALID");
        }

        var document = await documentsClient.DownloadAsync(envelope.SourceDocumentId, cancellationToken);
        return Result.Success(new EnvelopeDocumentDownload(document.FileName, document.ContentType, document.Content));
    }
}
