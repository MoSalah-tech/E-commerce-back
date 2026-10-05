using FluentValidation;
using MediatR;
using ValidationException = ECommerce.Application.Exceptions.ValidationException;

namespace ECommerce.Application.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request before the
/// handler executes, and converts failures into a single 400-shaped exception.
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
        ArgumentNullException.ThrowIfNull(next);

        var registered = validators as IList<IValidator<TRequest>> ?? validators.ToList();

        if (registered.Count == 0)
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            registered.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .Select(failure => failure.ErrorMessage)
            .Distinct()
            .ToArray();

        if (failures.Length > 0)
            throw new ValidationException(failures);

        return await next();
    }
}