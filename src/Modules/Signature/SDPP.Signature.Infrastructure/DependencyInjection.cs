using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Infrastructure.Messaging;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Signature.Application.Ports;
using SDPP.Signature.Infrastructure.Audit;
using SDPP.Signature.Infrastructure.Documents;
using SDPP.Signature.Infrastructure.Embedding;
using SDPP.Signature.Infrastructure.Identity;
using SDPP.Signature.Infrastructure.Notifications;
using SDPP.Signature.Infrastructure.Persistence;
using SDPP.Signature.Infrastructure.Security;
using SDPP.Signature.Infrastructure.Web;

namespace SDPP.Signature.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSignatureInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        services.AddDbContext<SignatureDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("SignatureDb"));
            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddScoped<ISignatureEnvelopeRepository, SignatureEnvelopeRepository>();
        services.AddScoped<ISavedSignatureRepository, SavedSignatureRepository>();
        services.AddScoped<ISignerAccessChallengeRepository, SignerAccessChallengeRepository>();
        services.AddScoped<ISignatureKeyRepository, SignatureKeyRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<OutboxProcessor<SignatureDbContext>>();
        services.AddScoped<SDPP.Signature.Application.UseCases.EnvelopeLifecycle.EnvelopeLifecycleJob>();

        services.AddScoped<IPdfEnvelopeEmbeddingEngine, PdfSharpEnvelopeEmbeddingEngine>();
        services.AddScoped<IPublicWebLinkBuilder, ConfigurationPublicWebLinkBuilder>();
        services.AddScoped<IKeyManagementService, DatabaseKeyManagementService>();
        services.AddSingleton<ITimestampAuthorityService, ServerTimestampAuthorityService>();
        services.AddSingleton<IOrganizationContextProvider, DefaultOrganizationContextProvider>();
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        // Real SMTP delivery activates automatically the moment Smtp:Host is configured — no code
        // change, no redeploy of a different image, just filling in the connection string. Until
        // then, LoggingEmailSender keeps every "email" attempt visible in Seq without pretending
        // to have sent anything (see IEmailSender's doc comment).
        var smtpConfigured = configuration.GetSection(SmtpOptions.SectionName).GetValue<string>("Host") is { Length: > 0 };
        if (smtpConfigured)
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }
        services.AddScoped<ILegalApprovalStampPolicy, ConfiguredLegalApprovalStampPolicy>();

        services.AddSdppAccessTokenForwarding();
        services.AddSdppInternalServiceKey();

        // Chains InternalServiceKeyHandler alongside the usual ForwardAccessTokenHandler — calls
        // made on behalf of an external envelope recipient (no SDPP session at all) still need to
        // authenticate to Documents.Api; see InternalServiceKeyFilter on the receiving side.
        services.AddHttpClient<IDocumentsClient, HttpDocumentsClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:DocumentsApi"]!);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<ForwardAccessTokenHandler>()
          .AddHttpMessageHandler<InternalServiceKeyHandler>()
          .AddStandardResilienceHandler();

        // Only called from SendEnvelope, always in the creator's own authenticated request — no
        // InternalServiceKeyHandler needed here (see HttpIdentityClient's doc comment).
        services.AddHttpClient<IIdentityClient, HttpIdentityClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:IdentityApi"]!);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<ForwardAccessTokenHandler>()
          .AddStandardResilienceHandler();

        // Only ever called from the public, AllowAnonymous /verify endpoint — see HttpAuditClient's
        // doc comment for why only the internal key (never a forwarded session) is needed here.
        services.AddHttpClient<IAuditClient, HttpAuditClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:AuditApi"]!);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddHttpMessageHandler<InternalServiceKeyHandler>()
          .AddStandardResilienceHandler();

        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        services.AddMassTransit(bus =>
        {
            // Signature only publishes envelope events — no consumers of its own.
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
