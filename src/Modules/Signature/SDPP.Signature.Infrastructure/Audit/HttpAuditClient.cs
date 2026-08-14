using System.Net.Http.Json;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Audit;

/// <summary>
/// HTTP implementation of Signature's outbound port to Audit.Api — mirrors HttpDocumentsClient's
/// registration pattern, but chains ONLY InternalServiceKeyHandler (no ForwardAccessTokenHandler):
/// this is called from the public, AllowAnonymous /verify endpoint, which may have no SDPP session
/// at all behind it, and Audit's own /records/integrity endpoint is itself internal-key-gated (see
/// AuditEndpoints.VerifyIntegrityAsync), never reachable by a forwarded user session anyway.
/// </summary>
public sealed class HttpAuditClient(HttpClient httpClient) : IAuditClient
{
    public async Task<AuditIntegrityCheck> VerifyIntegrityAsync(IReadOnlyList<Guid> subjectIds, CancellationToken cancellationToken = default)
    {
        var query = string.Join('&', subjectIds.Select(id => $"subjectId={id}"));
        var response = await httpClient.GetAsync($"/api/v1/audit/records/integrity?{query}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<IntegrityResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Respuesta vacía al verificar la integridad de la auditoría.");

        return new AuditIntegrityCheck(body.IsIntact, body.RecordCount);
    }

    public async Task<IReadOnlyList<AuditTrailRecord>> GetRecordsAsync(IReadOnlyList<Guid> subjectIds, CancellationToken cancellationToken = default)
    {
        var query = string.Join('&', subjectIds.Select(id => $"subjectId={id}"));
        var response = await httpClient.GetAsync($"/api/v1/audit/records/export?{query}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<AuditTrailRecord>>(cancellationToken: cancellationToken);
        return body ?? [];
    }

    private sealed record IntegrityResponse(bool IsIntact, int RecordCount, IReadOnlyList<long> BrokenRecordIds);
}
