using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Infrastructure.Messaging;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Classification.Application.Ports;
using SDPP.Classification.Application.Services;
using SDPP.Classification.Infrastructure.DocumentContent;
using SDPP.Classification.Infrastructure.Messaging;
using SDPP.Classification.Infrastructure.Persistence;
using SDPP.Classification.Infrastructure.Protection;

namespace SDPP.Classification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClassificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        services.AddDbContext<ClassificationDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("ClassificationDb"));
            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddScoped<IDlpRuleRepository, DlpRuleRepository>();
        services.AddScoped<IClassificationPolicyRepository, ClassificationPolicyRepository>();
        services.AddScoped<IInspectionResultRepository, InspectionResultRepository>();
        services.AddScoped<IDocumentIntegrityRecordRepository, DocumentIntegrityRecordRepository>();
        services.AddScoped<IDocumentVersionFingerprintRepository, DocumentVersionFingerprintRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<OutboxProcessor<ClassificationDbContext>>();

        services.AddSdppAccessTokenForwarding();
        services.AddHttpClient<IDocumentContentClient, HttpDocumentContentClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:DocumentsApi"]!);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpMessageHandler<ForwardAccessTokenHandler>()
          .AddStandardResilienceHandler();

        // Moved here from Documents.Infrastructure as part of the "Clasificación de Activos de
        // Información" extraction — Classification now owns fingerprinting/change-detection.
        services.AddScoped<IContentFingerprintService, ContentFingerprintService>();
        services.AddScoped<IChangeDetectionService, ChangeDetectionService>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<Messaging.DocumentUploadedIntegrityConsumer>();
            bus.AddConsumer<Messaging.ConversionCompletedIntegrityConsumer>();
            bus.AddConsumer<Messaging.ProtectionAppliedIntegrityConsumer>();
            bus.AddConsumer<Messaging.SignatureEnvelopeCompletedIntegrityConsumer>();

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

    /// <summary>Slim registration for the Conversion Worker: only the watermark/protection stack
    /// (moved here from Documents.Infrastructure) — no ClassificationDbContext, no MassTransit
    /// consumers, no HTTP clients. The worker never talks to SDPP_Classification directly; its
    /// only output is the ProtectionAppliedV1/ConversionCompletedV1 events it already publishes,
    /// which Classification.Api consumes asynchronously (see Messaging/IntegrityEventConsumers.cs).
    /// Self-contained (registers its own IIntegrationEventPublisher) so it doesn't depend on
    /// registration order against the worker's separate AddDocumentsInfrastructure call.</summary>
    public static IServiceCollection AddClassificationProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ProtectionPolicyConfig>(configuration.GetSection("ProtectionPolicies"));
        services.AddSingleton<IWatermarkContentBuilder, WatermarkContentBuilder>();
        services.AddScoped<IProtectionStampingEngine, PdfProtectionStampingEngine>();
        services.AddScoped<IPdfPermissionRestrictor, QpdfPermissionRestrictor>();
        services.AddScoped<IDocumentIntegritySigner, HmacDocumentIntegritySigner>();
        services.AddScoped<INotificationSender, AuditLoggingNotificationSender>();
        services.AddScoped<IProtectionEngine, ProtectionEngine>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        return services;
    }
}
