using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SDPP.Audit.Application.Ports;
using SDPP.Audit.Infrastructure.Messaging;
using SDPP.Audit.Infrastructure.Persistence;
using SDPP.BuildingBlocks.Application;

namespace SDPP.Audit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("AuditDb")));

        services.AddScoped<IAuditRecordRepository, AuditRecordRepository>();
        services.AddScoped<IUnitOfWork, AuditEfUnitOfWork>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<DocumentUploadedConsumer>();
            bus.AddConsumer<ConversionCompletedConsumer>();
            bus.AddConsumer<ConversionFailedConsumer>();
            bus.AddConsumer<DocumentBlockedConsumer>();
            bus.AddConsumer<ConversionBlockedConsumer>();
            bus.AddConsumer<ProtectionAppliedConsumer>();
            bus.AddConsumer<AdminNotificationRequestedConsumer>();
            bus.AddConsumer<DocumentVersionCreatedConsumer>();
            bus.AddConsumer<SignatureEnvelopeSentConsumer>();
            bus.AddConsumer<EnvelopeRecipientViewedConsumer>();
            bus.AddConsumer<EnvelopeRecipientSignedConsumer>();
            bus.AddConsumer<EnvelopeRecipientDeclinedConsumer>();
            bus.AddConsumer<SignatureEnvelopeCompletedConsumer>();
            bus.AddConsumer<SignatureEnvelopeCancelledConsumer>();
            bus.AddConsumer<SignatureEnvelopeDocumentAttachedConsumer>();
            bus.AddConsumer<RecipientOtpRequestedConsumer>();
            bus.AddConsumer<RecipientOtpValidatedConsumer>();
            bus.AddConsumer<SignatureEnvelopeExpiredConsumer>();
            bus.AddConsumer<CertificateGeneratedConsumer>();
            bus.AddConsumer<EnvelopeVerificationPerformedConsumer>();
            bus.AddConsumer<AccessAttemptRecordedConsumer>();
            bus.AddConsumer<UserStatusChangedConsumer>();
            bus.AddConsumer<UserRolesChangedConsumer>();
            bus.AddConsumer<SessionLoggedOutConsumer>();

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration["RabbitMq:Host"], configuration["RabbitMq:VirtualHost"], h =>
                {
                    h.Username(configuration["RabbitMq:Username"] ?? "guest");
                    h.Password(configuration["RabbitMq:Password"] ?? "guest");
                });

                // Dedicated queue per module, consistent with the fan-out shown in
                // docs/01-architecture/c4-diagrams.md — Audit gets its own durable subscription
                // to every event type it cares about.
                cfg.ReceiveEndpoint("sdpp.audit.events", e =>
                {
                    e.ConfigureConsumer<DocumentUploadedConsumer>(context);
                    e.ConfigureConsumer<ConversionCompletedConsumer>(context);
                    e.ConfigureConsumer<ConversionFailedConsumer>(context);
                    e.ConfigureConsumer<DocumentBlockedConsumer>(context);
                    e.ConfigureConsumer<ConversionBlockedConsumer>(context);
                    e.ConfigureConsumer<ProtectionAppliedConsumer>(context);
                    e.ConfigureConsumer<AdminNotificationRequestedConsumer>(context);
                    e.ConfigureConsumer<DocumentVersionCreatedConsumer>(context);
                    e.ConfigureConsumer<SignatureEnvelopeSentConsumer>(context);
                    e.ConfigureConsumer<EnvelopeRecipientViewedConsumer>(context);
                    e.ConfigureConsumer<EnvelopeRecipientSignedConsumer>(context);
                    e.ConfigureConsumer<EnvelopeRecipientDeclinedConsumer>(context);
                    e.ConfigureConsumer<SignatureEnvelopeCompletedConsumer>(context);
                    e.ConfigureConsumer<SignatureEnvelopeCancelledConsumer>(context);
                    e.ConfigureConsumer<SignatureEnvelopeDocumentAttachedConsumer>(context);
                    e.ConfigureConsumer<RecipientOtpRequestedConsumer>(context);
                    e.ConfigureConsumer<RecipientOtpValidatedConsumer>(context);
                    e.ConfigureConsumer<SignatureEnvelopeExpiredConsumer>(context);
                    e.ConfigureConsumer<CertificateGeneratedConsumer>(context);
                    e.ConfigureConsumer<EnvelopeVerificationPerformedConsumer>(context);
                });
            });
        });

        return services;
    }
}
