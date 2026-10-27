using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Перший (зовнішній) behavior: структуровано логує кожну команду й запит, результат і час.
/// Стоїть перед ValidationBehavior, тож у лозі видно й запити, відхилені валідацією, —
/// у них немає запису «Handled», а PerformanceBehavior біля обробника не з'являється взагалі.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await next(ct);

            logger.LogInformation("Handled {RequestName} in {ElapsedMs} ms", requestName, ElapsedMs(started));
            logger.LogDebug("{RequestName} response {@Response}", requestName, response);
            return response;
        }
        catch (Exception exception)
        {
            // Причину відмови видно тут; HTTP-статус і ProblemDetails формує глобальний обробник у WebApi
            logger.LogWarning("{RequestName} failed after {ElapsedMs} ms with {ExceptionType}: {Reason}",
                requestName, ElapsedMs(started), exception.GetType().Name, exception.Message);
            throw;
        }
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
