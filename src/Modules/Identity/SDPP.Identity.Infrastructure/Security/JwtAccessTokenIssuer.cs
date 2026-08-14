using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SDPP.Identity.Application.Ports;

namespace SDPP.Identity.Infrastructure.Security;

/// <summary>
/// Issues the short-lived (15 min) access JWT stored in the <c>sdpp_at</c> cookie. Claim TYPES are
/// deliberately the exact literal strings/URIs <c>HttpCurrentActor</c> (shared across every
/// module, see BuildingBlocks.Infrastructure.Security) already reads — "sub", "name",
/// <see cref="ClaimTypes.Email"/>, "domain", "department", <see cref="ClaimTypes.Role"/> — combined
/// with <c>MapInboundClaims = false</c> on the shared JwtBearer setup (AddSdppCookieJwtBearer) so
/// ASP.NET Core never silently remaps "sub"/"name" to different claim types on the way in.
/// </summary>
public sealed class JwtAccessTokenIssuer(IConfiguration configuration) : IAccessTokenIssuer
{
    private const int AccessTokenLifetimeMinutes = 15;

    public IssuedAccessToken Issue(Guid userId, string fullName, string email, string domain, string? department, IReadOnlyCollection<string> roles)
    {
        var signingKey = configuration["Authentication:SigningKey"]
            ?? throw new InvalidOperationException("Falta configurar 'Authentication:SigningKey'.");
        var issuer = configuration["Authentication:Issuer"] ?? "sdpp-identity";
        var audience = configuration["Authentication:Audience"] ?? "sdpp-platform";

        var jti = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("name", fullName),
            new(ClaimTypes.Email, email),
            new("domain", domain),
            new("department", department ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, jti),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(issuer, audience, claims, now, expiresAtUtc, credentials);
        var rawToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new IssuedAccessToken(rawToken, jti, expiresAtUtc);
    }
}
