using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using SDPP.BuildingBlocks.Application;
using SDPP.BuildingBlocks.Infrastructure.Messaging;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Infrastructure.Classification;
using SDPP.Documents.Infrastructure.Engines;
using SDPP.Documents.Infrastructure.Persistence;
using SDPP.Documents.Infrastructure.Reporting;
using SDPP.Documents.Infrastructure.Security;
using SDPP.Documents.Infrastructure.Storage;

namespace SDPP.Documents.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Shared by both SDPP.Documents.Api (no consumers — <paramref name="configureConsumers"/>
    /// omitted) and SDPP.Conversion.Worker (registers its ConversionRequestedV1 consumer via
    /// <paramref name="configureConsumers"/>) since MassTransit only allows a single
    /// AddMassTransit call per DI container.
    /// </summary>
    public static IServiceCollection AddDocumentsInfrastructure(
        this IServiceCollection services, IConfiguration configuration, Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        services.AddDbContext<DocumentsDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DocumentsDb"));
            options.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddScoped<Application.Ports.IDocumentRepository, DocumentRepository>();
        services.AddScoped<Application.Ports.ILogicalDocumentRepository, LogicalDocumentRepository>();
        services.AddScoped<Application.Ports.IDocumentVersionRepository, DocumentVersionRepository>();
        services.AddScoped<BuildingBlocks.Application.IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<OutboxProcessor<DocumentsDbContext>>();

        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(configuration["ObjectStorage:Endpoint"])
            .WithCredentials(configuration["ObjectStorage:AccessKey"], configuration["ObjectStorage:SecretKey"])
            .WithSSL(configuration.GetValue("ObjectStorage:UseSsl", true))
            .Build());
        services.AddScoped<IBlobStorage, MinIoBlobStorage>();

        services.Configure<ClamAvOptions>(configuration.GetSection("ClamAv"));
        services.AddScoped<IVirusScanner, ClamAvVirusScanner>();

        services.AddSdppAccessTokenForwarding();
        services.AddHttpClient<IClassificationClient, HttpClassificationClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:ClassificationApi"]!);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).AddHttpMessageHandler<ForwardAccessTokenHandler>()
          .AddStandardResilienceHandler();

        services.AddScoped<IConversionEngine, LibreOfficeEngine>();
        services.AddScoped<IConversionEngine, PdfToImageEngine>();
        services.AddScoped<IConversionEngine, QpdfEngine>();
        services.AddScoped<IConversionEngine, GhostscriptCompressEngine>();
        services.AddScoped<IConversionEngine, TesseractOcrEngine>();
        services.AddScoped<IConversionEngine, PdfReconstructionEngine>();
        services.AddScoped<IConversionEngine, PdfSharpEngine>();
        services.AddScoped<ITextExtractor, PopplerTextExtractor>();
        services.AddScoped<ITextExtractor, DocxTextExtractor>();
        services.AddScoped<Application.Ports.IDocumentsReportingQueries, DocumentsReportingQueries>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        services.AddMassTransit(bus =>
        {
            configureConsumers?.Invoke(bus);

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
