using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application.Behaviors;
using SDPP.Classification.Application.Detectors;

namespace SDPP.Classification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddClassificationApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton<IDetector, RegexDetector>();
        services.AddSingleton<IDetector, KeywordDetector>();
        services.AddSingleton<IDetector, ChecksumDetector>();
        services.AddSingleton<DetectorRegistry>();

        services.AddSingleton<IRiskScoringEngine, RiskScoringEngine>();
        services.AddSingleton<ILabelEngine, LabelEngine>();
        // No real model wired up yet — see IAiClassifier.cs for why this is deliberately a no-op.
        services.AddSingleton<IAiClassifier, NoOpAiClassifier>();

        return services;
    }
}
