using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application.Behaviors;
using SDPP.Documents.Application.Ports;
using SDPP.Documents.Application.Services;

namespace SDPP.Documents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Fingerprinting/change-detection moved to Classification.Application — see the
        // "Clasificación de Activos de Información" extraction. Text extraction stays here: it's
        // a document-content-processing capability Documents owns and exposes to Classification
        // over HTTP (GET /api/v1/documents/{id}/extracted-text), not the other way around.
        services.AddScoped<IDocumentTextExtractionService, DocumentTextExtractionService>();

        return services;
    }
}
