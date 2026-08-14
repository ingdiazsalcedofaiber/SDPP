using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SDPP.BuildingBlocks.Application;

namespace SDPP.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Redis-backed implementation of <see cref="IRevokedTokenStore"/> — two independent key spaces:
/// <c>revoked:jti:{jti}</c> (single-token revocation, set on logout) and
/// <c>revoked:user:{userId}</c> (holds the UTC instant after which every token for that user is
/// considered revoked, set when an admin deactivates a user or strips their roles). Both are set
/// with a TTL so Redis self-cleans instead of growing forever. Fails open (logs + returns
/// "not revoked") on any Redis connectivity error — see the fail-open trade-off documented on
/// <see cref="IRevokedTokenStore"/> itself.
/// </summary>
public sealed class RedisRevokedTokenStore(IConnectionMultiplexer redis, ILogger<RedisRevokedTokenStore> logger) : IRevokedTokenStore
{
    private IDatabase Database => redis.GetDatabase();

    public async Task RevokeTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            await Database.StringSetAsync($"revoked:jti:{jti}", "1", ttl);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "No se pudo revocar el token {Jti} en Redis (fail-open).", jti);
        }
    }

    public async Task RevokeUserAsync(Guid userId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            await Database.StringSetAsync($"revoked:user:{userId}", DateTime.UtcNow.ToString("O"), ttl);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "No se pudo revocar los tokens del usuario {UserId} en Redis (fail-open).", userId);
        }
    }

    public async Task<bool> IsRevokedAsync(string jti, Guid userId, DateTime tokenIssuedAtUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await Database.KeyExistsAsync($"revoked:jti:{jti}"))
            {
                return true;
            }

            var userRevokedAt = await Database.StringGetAsync($"revoked:user:{userId}");
            return userRevokedAt.HasValue && DateTime.Parse(userRevokedAt!, null, System.Globalization.DateTimeStyles.RoundtripKind) >= tokenIssuedAtUtc;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogError(ex, "No se pudo consultar la lista de revocación en Redis para {Jti} (fail-open: se trata como no revocado).", jti);
            return false;
        }
    }
}

public static class TokenRevocationExtensions
{
    public static IServiceCollection AddSdppTokenRevocation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration["Redis:ConnectionString"] ?? "localhost:6379"));
        services.AddSingleton<IRevokedTokenStore, RedisRevokedTokenStore>();
        return services;
    }
}
