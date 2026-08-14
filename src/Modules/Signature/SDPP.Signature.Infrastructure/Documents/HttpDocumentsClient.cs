using System.Net.Http.Json;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Documents;

/// <summary>
/// HTTP implementation of Signature's outbound port to Documents.Api — mirrors
/// HttpClassificationClient's registration pattern (Polly resilience + access-token forwarding,
/// see DependencyInjection.cs). Signature never touches SDPP_Documents directly. The HttpClient
/// this is registered against also chains InternalServiceKeyHandler (alongside the usual
/// ForwardAccessTokenHandler) so calls made on behalf of an external recipient with no SDPP
/// session still authenticate — see InternalServiceKeyFilter on the Documents.Api side.
/// </summary>
public sealed class HttpDocumentsClient(HttpClient httpClient) : IDocumentsClient
{
    private const string DocumentVersionIdHeader = "X-SDPP-Document-Version-Id";

    private sealed record SignedVersionResponse(Guid DocumentId, Guid DocumentVersionId, Guid SourceDocumentVersionId, string Sha256Hash);

    public async Task<DownloadedDocument> DownloadAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/v1/documents/{documentId}/download", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? "documento.pdf";
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
        var documentVersionId = response.Headers.TryGetValues(DocumentVersionIdHeader, out var values) && Guid.TryParse(values.FirstOrDefault(), out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Documents.Api no devolvió el encabezado '{DocumentVersionIdHeader}'.");
        var content = await response.Content.ReadAsStreamAsync(cancellationToken);

        return new DownloadedDocument(fileName, contentType, documentVersionId, content);
    }

    public async Task<SignedVersionResult> UploadSignedVersionAsync(
        Guid documentId, Stream content, string fileName, string contentType, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", fileName);
        form.Add(new StringContent(createdByUserId.ToString()), "createdByUserId");

        var response = await httpClient.PostAsync($"/api/v1/documents/{documentId}/signed-version", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<SignedVersionResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Respuesta vacía al registrar la versión firmada.");

        return new SignedVersionResult(body.DocumentId, body.DocumentVersionId, body.SourceDocumentVersionId, body.Sha256Hash);
    }

    public async Task LockAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/api/v1/documents/{documentId}/lock", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
