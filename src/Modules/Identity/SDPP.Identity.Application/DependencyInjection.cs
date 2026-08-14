using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SDPP.BuildingBlocks.Application.Behaviors;
using SDPP.Identity.Application.Services;

namespace SDPP.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<ILoginCompletionService, LoginCompletionService>();

        return services;
    }
}
