using System.Diagnostics;
using Grpc.Core;
using Platform.Aggregator.Resilience;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Спільна частина gRPC-клієнтів агрегатора (замість HTTP-базового UpstreamClient з Лаб. 05):
/// логування кожного виклику («Calling courses-api gRPC GetCoursesByIds» -> «courses-api responded OK
/// in 4.2 ms»), deadline і перетворення NOT_FOUND на null. Решта статусів (UNAVAILABLE,
/// DEADLINE_EXCEEDED...) виходить як RpcException, і AggregationContext робить з неї попередження.
///
/// Канал і HttpClient дає фабрика gRPC-клієнтів (AddGrpcClient) — з тими самими налаштуваннями
/// ServiceDefaults, що й у HttpClient Лаб. 05: service discovery, resilience і CorrelationIdHandler,
/// який кладе X-Correlation-Id у метадані (заголовки HTTP/2) кожного виклику.
/// </summary>
public abstract class GrpcUpstream(ILogger logger)
{
    /// <summary>Політика стійкості сервісу (Лаб. 07): критичність, повтори, breaker, таймаути.</summary>
    protected abstract ServiceResiliencePolicy Policy { get; }

    /// <summary>Логічне ім'я сервісу в AppHost — воно ж у логах і попередженнях.</summary>
    public string ServiceName => Policy.Service;

    /// <summary>
    /// Deadline — верхня межа виклику разом з усіма повторами (gRPC RetryPolicy і HTTP-обробника).
    /// Він передається й серверу: той перестає працювати над запитом, на відповідь на який уже ніхто не чекає.
    /// </summary>
    protected CallOptions Options(CancellationToken ct) => new(deadline: DateTime.UtcNow.Add(Policy.CallDeadline), cancellationToken: ct);

    /// <summary>Виклик із логуванням; NOT_FOUND -> null («такого запису немає», а не відмова сервісу).</summary>
    protected async Task<T?> CallAsync<T>(string method, Func<AsyncUnaryCall<T>> call) where T : class
    {
        logger.LogInformation("Calling {Service} gRPC {GrpcMethod}", ServiceName, method);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await call();
            logger.LogInformation("{Service} responded {GrpcStatus} to gRPC {GrpcMethod} in {ElapsedMs} ms",
                ServiceName, StatusCode.OK, method, ElapsedMs(started));
            return response;
        }
        catch (RpcException exception)
        {
            logger.LogInformation("{Service} responded {GrpcStatus} to gRPC {GrpcMethod} in {ElapsedMs} ms",
                ServiceName, exception.StatusCode, method, ElapsedMs(started));
            if (exception.StatusCode == StatusCode.NotFound)
                return null;
            throw;
        }
    }

    private static double ElapsedMs(long started) => Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);
}
