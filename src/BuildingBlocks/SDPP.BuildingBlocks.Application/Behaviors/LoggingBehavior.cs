using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SDPP.BuildingBlocks.Application.Behaviors;

/// <summary>Structured logging around every Command/Query, correlated for Serilog + OpenTelemetry.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling {RequestName}", requestName);
        try
        {
            var response = await next();
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Failed {RequestName} after {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
