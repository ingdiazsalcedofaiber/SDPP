using SDPP.BuildingBlocks.Application;
using SDPP.Documents.Domain.Aggregates;

namespace SDPP.Documents.Application.UseCases;

/// <summary>Same shape as Signature's EnvelopeAuthorization.CanManage: ownership or Administrador,
/// no dedicated role for "view any document". `currentActor.IsAuthenticated` is false only when the
/// caller reached the handler via InternalServiceKeyFilter's internal-key path (no user session at
/// all, e.g. Signature.Api relaying a download for an external envelope recipient) — that path is
/// already gated at the endpoint level, so it's trusted here without an ownership check; only a real
/// end-user session gets held to "do you actually own this document".</summary>
internal static class DocumentAuthorization
{
    public static bool CanView(DocumentInstance document, ICurrentActor currentActor) =>
        !currentActor.IsAuthenticated ||
        document.OwnerId == currentActor.UserId ||
        currentActor.Roles.Contains("Administrador");
}
