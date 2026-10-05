using MediatR;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Behaviors;

/// <summary>Structured logging around every request handler.</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var name = typeof(TRequest).Name;

        logger.LogInformation("Handling {RequestName} {@Request}", name, request);

        try
        {
            var response = await next();
            logger.LogInformation("Handled {RequestName} with response {@Response}", name, response);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Request {RequestName} failed", name);
            throw;
        }
    }
}