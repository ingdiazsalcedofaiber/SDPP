using Microsoft.Extensions.Configuration;
using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Security;

/// <summary>Reads the single authorized email from `Signature:LegalApprovalStampEmail` — same
/// "configured value, not hardcoded in domain/application code" shape as
/// DefaultOrganizationContextProvider. Case-insensitive since email addresses are.</summary>
public sealed class ConfiguredLegalApprovalStampPolicy(IConfiguration configuration) : ILegalApprovalStampPolicy
{
    public bool IsAuthorized(string email)
    {
        var configuredEmail = configuration["Signature:LegalApprovalStampEmail"];
        return !string.IsNullOrWhiteSpace(configuredEmail) && string.Equals(email, configuredEmail, StringComparison.OrdinalIgnoreCase);
    }
}
