using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Caching;

/// <summary>
/// Cache-aside для запитів CQRS (Лаб. 06): стоїть після ValidationBehavior — некоректний запит
/// до кешу не доходить. Для ICacheableQuery результат береться з HybridCache: L1 (IMemoryCache) ->
/// L2 (Redis через IDistributedCache) -> обробник (MongoDB); рівні заповнюються на зворотному шляху,
/// а одночасні промахи того самого ключа HybridCache зводить до одного звернення до бази.
/// Команди й некешовані запити проходять без змін. Обробники про кеш не знають.
///
/// Redis — прискорювач, а не залежність: якщо кеш кинув виняток (Redis недоступний), запит
/// виконується напряму в базі, а збій лише логується — сервіс відповідає повільніше, але не падає.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse>(
    HybridCache cache,
    ReviewsCacheMetrics metrics,
    ILogger<CachingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICacheableQuery { CacheKey: { } key } cacheable)
            return await next(ct);

        var queryName = typeof(TRequest).Name;
        using var activity = ReviewsCacheMetrics.ActivitySource.StartActivity("cache GetOrCreate");
        activity?.SetTag("cache.key", key);

        var started = Stopwatch.GetTimestamp();
        var loaded = false;
        var handlerFailed = false;
        TResponse? fromHandler = default;

        var options = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = cacheable.CachePolicy.Local,   // L1 коротший за L2
            Expiration = cacheable.CachePolicy.Distributed
        };

        // HybridCache виконує фабрику без ExecutionContext виклику (захист від stampede — одна фабрика
        // на всіх, хто чекає цей ключ). Без відновлення контексту span MongoDB ставав би окремим trace,
        // а логи обробника втрачали б CorrelationId. Тому фабрика запускається в захопленому контексті.
        var callerContext = ExecutionContext.Capture();

        TResponse response;
        try
        {
            response = await cache.GetOrCreateAsync(key, token => RunInContext(callerContext, async () =>
            {
                loaded = true;
                try
                {
                    return fromHandler = await next(token);
                }
                catch
                {
                    handlerFailed = true;   // помилку обробника (NotFound, валідація) не маскуємо
                    throw;
                }
            }), options, cacheable.CacheTags, ct);
        }
        catch (Exception exception) when (!handlerFailed && !ct.IsCancellationRequested)
        {
            metrics.Failed(queryName);
            activity?.SetTag("cache.result", "error");
            logger.LogWarning("Cache {CacheKey} unavailable ({FailureType}: {FailureMessage}); {RequestName} served from MongoDB",
                key, exception.GetType().Name, exception.GetBaseException().Message, queryName);

            // Обробник уже відпрацював (впав лише запис у Redis) — повторно в базу не йдемо
            return loaded && fromHandler is not null ? fromHandler : await next(ct);
        }

        var elapsedMs = Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);
        metrics.Duration(!loaded, elapsedMs);
        activity?.SetTag("cache.result", loaded ? "miss" : "hit");

        if (loaded)
        {
            metrics.Miss(queryName);
            logger.LogInformation("Cache miss {CacheKey} for {RequestName}: loaded from MongoDB in {ElapsedMs} ms, L1 TTL {L1Ttl}, L2 TTL {L2Ttl}",
                key, queryName, elapsedMs, cacheable.CachePolicy.Local, cacheable.CachePolicy.Distributed);
        }
        else
        {
            metrics.Hit(queryName);
            logger.LogDebug("Cache hit {CacheKey} for {RequestName} in {ElapsedMs} ms", key, queryName, elapsedMs);
        }

        return response;
    }

    /// <summary>
    /// Запускає асинхронну фабрику в заданому ExecutionContext: синхронна частина виконується всередині
    /// ExecutionContext.Run, а продовження після await підхоплюють уже цей контекст
    /// (Activity.Current, LogContext Serilog, HttpContext).
    /// </summary>
    private static ValueTask<TResponse> RunInContext(ExecutionContext? context, Func<Task<TResponse>> factory)
    {
        if (context is null)
            return new ValueTask<TResponse>(factory());

        Task<TResponse>? task = null;
        ExecutionContext.Run(context, _ => task = factory(), null);
        return new ValueTask<TResponse>(task!);
    }
}
