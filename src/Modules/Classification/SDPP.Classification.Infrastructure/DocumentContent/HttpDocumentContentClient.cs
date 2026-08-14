using System.Net.Http.Json;
using SDPP.Classification.Application.Ports;

namespace SDPP.Classification.Infrastructure.DocumentContent;

/// <summary>
/// Calls back into Document API to fetch the already-extracted text of a document (the Documents
/// module owns text extraction from Office/PDF binaries — see
/// docs/01-architecture/c4-diagrams.md, "solicita inspección previa (sync)"). Authenticated by
/// forwarding the current user's access token as an Authorization: Bearer header — see
/// ForwardAccessTokenHandler in BuildingBlocks.Infrastructure.Security — since the platform has no
/// separate service-account/client-credentials flow.
/// </summary>
public sealed class HttpDocumentContentClient(HttpClient httpClient) : IDocumentContentClient
{
    private sealed record ContentResponse(string FileName, string ExtractedText, Dictionary<string, string> Metadata);

    public async Task<DocumentContentForInspection> GetContentForInspectionAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/v1/documents/{documentId}/extracted-text", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ContentResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Respuesta vacía al solicitar el contenido extraído del documento.");

        return new DocumentContentForInspection(body.FileName, body.ExtractedText, body.Metadata);
    }
}
