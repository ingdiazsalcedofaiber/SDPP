using System.Net.Http.Json;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Domain.Enums;

namespace SDPP.Documents.Infrastructure.Classification;

/// <summary>
/// HTTP implementation of the synchronous call from Document API to Classification &amp; DLP API
/// (now also "Clasificación de Activos de Información") shown in docs/01-architecture/c4-diagrams.md
/// (component diagram) and docs/05-security/classification-engine.md §3. Registered with a Polly
/// resilience pipeline (see DependencyInjection.cs) so that a timeout or 5xx response surfaces as
/// an exception the caller treats as "fail closed" rather than an implicit Allow.
/// </summary>
public sealed class HttpClassificationClient(HttpClient httpClient) : IClassificationClient
{
    private sealed record ClassifyResponse(
        string Classification, string ClassificationSource, bool RequiresManualReview, int RiskScore, string? Category,
        string[] Labels, string? ContentFingerprint, string? StructuralSignature, string ChangeType, Guid? InheritedFromVersionId);

    private sealed record VersionSummaryResponse(string? Classification, int? RiskScore, string? Category, string[] Labels);

    private sealed record DocumentIntegrityResponse(Guid DocumentId, string? IntegritySignature, string[] ProtectionsApplied);

    public async Task<ClassifyDocumentDecision> ClassifyAsync(
        Guid documentId, Guid documentVersionId, Guid logicalDocumentId, string contentType, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/api/v1/classification/documents/{documentId}/classify",
            new { documentVersionId, logicalDocumentId, contentType },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ClassifyResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Respuesta vacía del servicio de clasificación.");

        return new ClassifyDocumentDecision(
            Enum.Parse<ClassificationLevel>(body.Classification), body.ClassificationSource, body.RequiresManualReview,
            body.RiskScore, body.Category, body.Labels, body.ContentFingerprint, body.StructuralSignature,
            body.ChangeType, body.InheritedFromVersionId);
    }

    public async Task<VersionSummary> GetVersionSummaryAsync(Guid documentVersionId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/v1/classification/versions/{documentVersionId}/summary", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<VersionSummaryResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Respuesta vacía al consultar el resumen de clasificación.");

        return new VersionSummary(
            body.Classification is null ? null : Enum.Parse<ClassificationLevel>(body.Classification),
            body.RiskScore, body.Category, body.Labels);
    }

    public async Task<IReadOnlyList<DocumentIntegritySummary>> GetBatchIntegrityAsync(
        IReadOnlyList<Guid> documentIds, CancellationToken cancellationToken = default)
    {
        if (documentIds.Count == 0)
        {
            return [];
        }

        var query = string.Join('&', documentIds.Select(id => $"ids={id}"));
        var response = await httpClient.GetAsync($"/api/v1/classification/documents/batch-integrity?{query}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DocumentIntegrityResponse[]>(cancellationToken)
            ?? throw new InvalidOperationException("Respuesta vacía al consultar la integridad de documentos.");

        return body.Select(r => new DocumentIntegritySummary(r.DocumentId, r.IntegritySignature, r.ProtectionsApplied)).ToList();
    }
}
