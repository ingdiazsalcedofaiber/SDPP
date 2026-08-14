using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Populates ICurrentActor strictly from validated JWT claims (issued by Identity.Api, see
/// SDPP.Identity.Infrastructure.Security.JwtAccessTokenIssuer) plus HttpContext connection info —
/// never from anything the client can set directly in the request body (see
/// docs/05-security/threat-model-stride.md, E1). Shared by every module's Api project instead of
/// being duplicated per-service — previously lived only in Documents.Api.
/// </summary>
public sealed class HttpCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    private HttpContext Context => httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No hay un HttpContext activo.");

    public bool IsAuthenticated => Context.User.Identity?.IsAuthenticated ?? false;

    public Guid UserId => Guid.TryParse(Context.User.FindFirstValue("sub") ?? Context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new InvalidOperationException("El token no contiene un identificador de usuario válido.");

    public string FullName => Context.User.FindFirstValue("name") ?? "Desconocido";
    public string Email => Context.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public string Domain => Context.User.FindFirstValue("domain") ?? string.Empty;
    public string Department => Context.User.FindFirstValue("department") ?? string.Empty;

    public IReadOnlyCollection<string> Roles =>
        Context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    public string? IpAddress => Context.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Context.Request.Headers.UserAgent.ToString();
}

public static class CurrentActorExtensions
{
    public static IServiceCollection AddSdppCurrentActor(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpCurrentActor>();
        return services;
    }
}
