using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application.Behaviors;

namespace SDPP.Signature.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSignatureApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
