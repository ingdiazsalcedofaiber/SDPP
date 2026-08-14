using Microsoft.Extensions.Configuration;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Web;

public sealed class ConfigurationPublicWebLinkBuilder(IConfiguration configuration) : IPublicWebLinkBuilder
{
    public string BuildVerificationUrl(Guid envelopeId)
    {
        var baseUrl = (configuration["PublicWebBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        return $"{baseUrl}/firmar/verificar/{envelopeId}";
    }
}
