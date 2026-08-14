using System.Net;
using System.Net.Http.Json;
using System.Web;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Identity;

/// <summary>HTTP implementation of Signature's outbound port to Identity.Api — same
/// HttpClient+Polly+token-forwarding registration pattern as HttpDocumentsClient. Only called from
/// SendEnvelope, always in the context of the creator's own authenticated request, so no
/// InternalServiceKeyHandler dependency here (unlike HttpDocumentsClient, which is also called from
/// the public signer-access flow).</summary>
public sealed class HttpIdentityClient(HttpClient httpClient) : IIdentityClient
{
    public async Task<IdentityUserLookup?> LookupByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/v1/identity/users/lookup?email={HttpUtility.UrlEncode(email)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IdentityUserLookup>(cancellationToken: cancellationToken);
    }
}
