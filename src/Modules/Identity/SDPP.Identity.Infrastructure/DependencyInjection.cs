using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Infrastructure.Messaging;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Identity.Application.Configuration;
using SDPP.Identity.Application.Ports;
using SDPP.Identity.Infrastructure.Google;
using SDPP.Identity.Infrastructure.Mfa;
using SDPP.Identity.Infrastructure.Persistence;
using SDPP.Identity.Infrastructure.Reporting;
using SDPP.Identity.Infrastructure.Security;

namespace SDPP.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("IdentityDb"));
            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAllowedDomainRepository, AllowedDomainRepository>();
        services.AddScoped<IAccessAuditLogRepository, AccessAuditLogRepository>();
        services.AddScoped<IMfaChallengeRepository, MfaChallengeRepository>();
        services.AddScoped<IUsersReportingQueries, UsersReportingQueries>();
        services.AddScoped<IUnitOfWork, IdentityEfUnitOfWork>();

        services.Configure<IdentityPolicyOptions>(configuration.GetSection("Identity"));
        services.AddScoped<IGoogleTokenValidator, GoogleJsonWebSignatureTokenValidator>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<IMfaSecretProtector, AesMfaSecretProtector>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        services.AddSdppTokenRevocation(configuration);

        services.AddMassTransit(bus =>
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration["RabbitMq:Host"], configuration["RabbitMq:VirtualHost"], h =>
                {
                    h.Username(configuration["RabbitMq:Username"] ?? "guest");
                    h.Password(configuration["RabbitMq:Password"] ?? "guest");
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
