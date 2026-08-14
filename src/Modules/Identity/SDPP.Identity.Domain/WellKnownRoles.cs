namespace SDPP.Identity.Domain;

/// <summary>
/// Exact role-name spelling every existing RBAC check in the platform already relies on —
/// <c>HttpCurrentActor.IsInRole</c> and Audit.Api's <c>AuditorOrAdmin</c> policy
/// (<c>RequireRole("Auditor","Administrador")</c>) compare role claim values case-sensitively, so
/// seeding anything other than this exact casing (e.g. the literal "ADMINISTRADOR"/"USUARIO" from
/// a spec) would silently break policies that already work today.
/// </summary>
public static class WellKnownRoles
{
    public const string Usuario = "Usuario";
    public const string Auditor = "Auditor";
    public const string Administrador = "Administrador";
}
