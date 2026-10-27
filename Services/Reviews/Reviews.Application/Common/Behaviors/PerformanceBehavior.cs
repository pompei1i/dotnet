using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

/// <summary>Поріг «повільного» обробника; задається Application:SlowRequestThresholdMs.</summary>
public sealed record PerformanceSettings(int SlowRequestThresholdMs);

/// <summary>
/// Найближчий до обробника behavior: вимірює час самого обробника разом із запитами до MongoDB
/// і пише попередження, якщо він довший за поріг. Запит, відхилений валідацією, сюди не доходить.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    PerformanceSettings settings,
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await next(ct);
        var elapsedMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (elapsedMs > settings.SlowRequestThresholdMs)
            logger.LogWarning("Slow handler {RequestName}: {ElapsedMs} ms (threshold {ThresholdMs} ms) {@Request}",
                typeof(TRequest).Name, elapsedMs, settings.SlowRequestThresholdMs, request);
        else
            logger.LogDebug("Handler {RequestName} executed in {ElapsedMs} ms", typeof(TRequest).Name, elapsedMs);

        return response;
    }
}
