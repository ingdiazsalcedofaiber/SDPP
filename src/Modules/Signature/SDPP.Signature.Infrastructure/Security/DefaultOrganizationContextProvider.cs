using SDPP.Signature.Application.Ports;

namespace SDPP.Signature.Infrastructure.Security;

/// <summary>Today's only IOrganizationContextProvider — see the port's doc comment. Every real user
/// in this deployment belongs to this one default organization until Identity gains real
/// multi-tenant support.</summary>
public sealed class DefaultOrganizationContextProvider : IOrganizationContextProvider
{
    public static readonly Guid DefaultOrganizationId = new("00000000-0000-0000-0000-000000000001");

    public Guid GetCurrentOrganizationId() => DefaultOrganizationId;
}
