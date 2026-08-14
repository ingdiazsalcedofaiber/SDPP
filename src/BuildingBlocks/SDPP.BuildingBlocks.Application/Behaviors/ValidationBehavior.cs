using FluentValidation;
using MediatR;

namespace SDPP.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Runs all FluentValidation validators registered for the request before the handler executes.
/// Applies to every Command/Query in the pipeline (defense in depth alongside domain invariants
/// and database constraints, see docs/00-overview.md §4.2).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
