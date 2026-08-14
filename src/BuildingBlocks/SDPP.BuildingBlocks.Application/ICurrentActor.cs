namespace SDPP.BuildingBlocks.Application;

/// <summary>
/// The identity and request context of whoever is executing the current use case — a human user
/// authenticated via AD/OIDC, or an external system authenticated via client_credentials.
/// Populated by the API layer from validated token claims and HTTP context; never trusted from
/// client-supplied payload fields (see docs/05-security/threat-model-stride.md, E1).
/// </summary>
public interface ICurrentActor
{
    Guid UserId { get; }
    string FullName { get; }
    string Email { get; }
    string Domain { get; }
    string Department { get; }
    IReadOnlyCollection<string> Roles { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool IsAuthenticated { get; }

    bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
