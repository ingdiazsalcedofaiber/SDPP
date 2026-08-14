using SDPP.BuildingBlocks.Application;
using SDPP.Signature.Domain.Aggregates;

namespace SDPP.Signature.Application.UseCases;

/// <summary>No new role exists for envelope management — same criterion as every other Signature/
/// Documents/Classification endpoint ("cualquier usuario autenticado puede firmar"). Creator actions
/// (configure/send/cancel) are gated by ownership instead, with Administrador as the standard
/// escape hatch already used elsewhere in the platform. Also enforces multi-tenant isolation
/// (organizationId): even an Administrador of one organization must never manage another
/// organization's envelope — see IOrganizationContextProvider's doc comment for why this is
/// currently always a single fixed value, and why the check exists anyway.</summary>
internal static class EnvelopeAuthorization
{
    public static bool CanManage(SignatureEnvelope envelope, ICurrentActor currentActor, Guid currentOrganizationId) =>
        envelope.OrganizationId == currentOrganizationId &&
        (envelope.CreatedByUserId == currentActor.UserId || currentActor.Roles.Contains("Administrador"));
}
