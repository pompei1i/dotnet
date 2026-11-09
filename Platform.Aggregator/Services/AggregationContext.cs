using System.Diagnostics;
using System.Text.Json;
using Grpc.Core;
using Platform.Aggregator.Models;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Platform.Aggregator.Services;

/// <summary>
/// Стан одного агрегувального запиту: обгортає виклики сервісів, міряє їхню тривалість і збирає
/// попередження. Виклики виконуються паралельно, тому спільні списки захищено блокуванням.
/// </summary>
public sealed class AggregationContext(ILogger logger, CancellationToken ct)
{
    private readonly Lock _gate = new();
    private readonly List<string> _warnings = [];
    private readonly List<SourceStatusDto> _sources = [];

    public IReadOnlyList<string> Warnings
    {
        get { lock (_gate) return [.. _warnings]; }
    }

    public IReadOnlyList<SourceStatusDto> Sources
    {
        get { lock (_gate) return [.. _sources.OrderBy(source => source.Service)]; }
    }

    /// <summary>
    /// Необов'язкові дані: недоступність сервісу не «валить» запит — результат null, а в Warnings
    /// з'являється «reviews-api is unavailable». Скасування самим клієнтом не ковтається.
    /// </summary>
    public async Task<T?> SafeAsync<T>(string service, Func<Task<T>> call)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await call();
            Succeeded(service, started);
            return result;
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            Failed(service, started, exception);
            Warn($"{service} is unavailable: {Describe(exception)}");
            return default;
        }
    }

    /// <summary>
    /// Обов'язкові дані (без замовлення картку не зібрати): недоступність сервісу -> 503
    /// через UpstreamUnavailableException, а не 500.
    /// </summary>
    public async Task<T> RequiredAsync<T>(string service, Func<Task<T>> call)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await call();
            Succeeded(service, started);
            return result;
        }
        catch (Exception exception) when (IsUpstreamFailure(exception))
        {
            Failed(service, started, exception);
            throw new UpstreamUnavailableException(service, Describe(exception), exception);
        }
    }

    /// <summary>Чи відповів сервіс у межах цього запиту (навіть якщо 404).</summary>
    public bool Responded(string service)
    {
        lock (_gate) return _sources.Any(source => source.Service == service && source.Status == "ok");
    }

    public void Warn(string warning)
    {
        lock (_gate) _warnings.Add(warning);
    }

    private void Succeeded(string service, long started)
    {
        var elapsedMs = ElapsedMs(started);
        logger.LogInformation("{Service} responded in {ElapsedMs} ms", service, elapsedMs);
        lock (_gate) _sources.Add(new SourceStatusDto(service, "ok", elapsedMs));
    }

    private void Failed(string service, long started, Exception exception)
    {
        var elapsedMs = ElapsedMs(started);
        logger.LogWarning(exception, "{Service} is unavailable after {ElapsedMs} ms: {FailureType} {FailureMessage}",
            service, elapsedMs, exception.GetType().Name, exception.GetBaseException().Message);
        lock (_gate) _sources.Add(new SourceStatusDto(service, "unavailable", elapsedMs));
    }

    /// <summary>
    /// Відмови сервісу. gRPC (Лаб. 06): RpcException зі статусом UNAVAILABLE (сервіс не відповідає,
    /// timeout чи circuit breaker resilience-обробника всередині HttpClient), DEADLINE_EXCEEDED,
    /// INTERNAL / UNKNOWN (збій на боці сервісу), RESOURCE_EXHAUSTED. Помилки самого запиту
    /// (INVALID_ARGUMENT, PERMISSION_DENIED) — баг агрегатора, а не недоступність, тож не ковтаються.
    /// Скасування запиту клієнтом (ct) — не відмова сервісу, воно пробрасується далі.
    /// </summary>
    private bool IsUpstreamFailure(Exception exception) => exception switch
    {
        RpcException { StatusCode: StatusCode.Cancelled } => !ct.IsCancellationRequested,
        RpcException rpc => rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded
            or StatusCode.Internal or StatusCode.Unknown or StatusCode.ResourceExhausted,
        HttpRequestException or TimeoutRejectedException or BrokenCircuitException or JsonException => true,
        OperationCanceledException => !ct.IsCancellationRequested,
        _ => false
    };

    private static string Describe(Exception exception) => exception switch
    {
        // Причина відмови HttpClient (timeout Polly, відмова з'єднання) лежить у DebugException
        RpcException { Status.DebugException: TimeoutRejectedException or OperationCanceledException } => "timeout",
        RpcException { Status.DebugException: BrokenCircuitException } => "circuit breaker is open",
        RpcException { StatusCode: StatusCode.DeadlineExceeded } => "timeout",
        RpcException { StatusCode: StatusCode.Unavailable } => "unavailable",
        RpcException rpc => $"gRPC {rpc.StatusCode}",
        HttpRequestException { StatusCode: { } status } => $"HTTP {(int)status}",
        HttpRequestException http => http.HttpRequestError.ToString(),
        TimeoutRejectedException or OperationCanceledException => "timeout",
        BrokenCircuitException => "circuit breaker is open",
        JsonException => "invalid response",
        _ => exception.GetType().Name
    };

    private static double ElapsedMs(long started) => Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);
}
