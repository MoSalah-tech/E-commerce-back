using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECommerce.Application.Behaviors;

/// <summary>Warns when a handler runs longer than the configured budget.</summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Handlers slower than this are logged as a warning.</summary>
    public const int LongRunningThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var timer = Stopwatch.StartNew();
        var response = await next();
        timer.Stop();

        var elapsed = timer.ElapsedMilliseconds;
        if (elapsed > LongRunningThresholdMs)
        {
            logger.LogWarning(
                "Long running handler {RequestName} took {ElapsedMs}ms",
                typeof(TRequest).Name,
                elapsed);
        }

        return response;
    }
}