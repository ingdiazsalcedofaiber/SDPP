namespace SDPP.Identity.Application.Ports;

public sealed record IssuedAccessToken(string Token, string Jti, DateTime ExpiresAtUtc);

/// <summary>
/// Issues the short-lived JWT stored in the <c>sdpp_at</c> cookie. Claims are deliberately fixed
/// to exactly what <c>HttpCurrentActor</c> (shared across every module) already reads —
/// <c>sub</c>/<c>name</c>/email/<c>domain</c>/<c>department</c>/role claims — so nothing downstream
/// needs to change to consume identities issued by this module.
/// </summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(Guid userId, string fullName, string email, string domain, string? department, IReadOnlyCollection<string> roles);
}
