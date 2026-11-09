using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Net.Http.Headers;

namespace Platform.Aggregator.Caching;

/// <summary>
/// Кеш складених відповідей агрегатора в Redis (IDistributedCache, Лаб. 06).
///
/// - Ключ — із параметрів запиту: "aggregator:order:{id}:full", "aggregator:course:{id}:overview".
/// - Перевіряється ДО паралельних gRPC-викликів: попадання не звертається до жодного сервісу.
/// - TTL 30 с — коротший за кеші окремих сервісів (хвилини): інакше агрегована відповідь виглядала б
///   «свіжішою», ніж дані, з яких її зібрано. Явної інвалідації немає — агрегатор не знає про записи
///   в сервісах, тож узгодженість забезпечує саме короткий TTL.
/// - Часткові відповіді (з Warnings) не кешуються: інакше 30 с усі бачили б картку без відгуків,
///   навіть коли reviews-api уже піднявся.
/// - Заголовок запиту Cache-Control: no-cache — оминути кеш (так міряється чиста швидкість gRPC-агрегації).
/// - Redis недоступний — агрегація йде напряму, збій лише логується.
/// </summary>
public sealed class AggregateCache(
    IDistributedCache redis,
    AggregateCacheMetrics metrics,
    IHttpContextAccessor http,
    ILogger<AggregateCache> logger)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string OrderCardKey(long orderId) => $"aggregator:order:{orderId}:full";

    public static string CourseOverviewKey(long courseId) => $"aggregator:course:{courseId}:overview";

    /// <param name="build">Агрегація через gRPC (виконується лише при промаху).</param>
    /// <param name="isComplete">Чи можна кешувати результат (немає попереджень).</param>
    public async Task<T?> GetOrBuildAsync<T>(string key, Func<Task<T?>> build, Func<T, bool> isComplete, CancellationToken ct)
        where T : class
    {
        using var activity = AggregateCacheMetrics.ActivitySource.StartActivity("cache GetOrCreate");
        activity?.SetTag("cache.key", key);

        if (Bypass())
        {
            activity?.SetTag("cache.result", "bypass");
            SetHeader("BYPASS");
            return await build();
        }

        var cached = await TryGetAsync<T>(key, ct);
        if (cached is not null)
        {
            metrics.Hit();
            activity?.SetTag("cache.result", "hit");
            SetHeader("HIT");
            logger.LogInformation("Aggregate cache hit {CacheKey}: no gRPC calls", key);
            return cached;
        }

        metrics.Miss();
        activity?.SetTag("cache.result", "miss");
        SetHeader("MISS");
        logger.LogInformation("Aggregate cache miss {CacheKey}: aggregating via gRPC", key);

        var result = await build();
        if (result is null)
            return null;

        if (!isComplete(result))
        {
            logger.LogInformation("Partial response for {CacheKey} is not cached", key);
            return result;
        }

        await TrySetAsync(key, result, ct);
        return result;
    }

    private bool Bypass()
        => http.HttpContext?.Request.Headers.CacheControl.ToString()
            .Contains(CacheControlHeaderValue.NoCacheString, StringComparison.OrdinalIgnoreCase) == true;

    private void SetHeader(string value)
    {
        if (http.HttpContext is { Response.HasStarted: false } context)
            context.Response.Headers["X-Cache"] = value;
    }

    private async Task<T?> TryGetAsync<T>(string key, CancellationToken ct) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var bytes = await redis.GetAsync(key, ct);
            metrics.Duration("get", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes, Json);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            metrics.Failed("get");
            logger.LogWarning("Redis get {CacheKey} failed ({FailureType}: {FailureMessage}); aggregating without cache",
                key, exception.GetType().Name, exception.GetBaseException().Message);
            return null;
        }
    }

    private async Task TrySetAsync<T>(string key, T value, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(value, Json);
            await redis.SetAsync(key, payload, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
            metrics.Duration("set", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            logger.LogInformation("Aggregate cached {CacheKey}: {SizeBytes} bytes for {CacheTtl}", key, payload.Length, Ttl);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            metrics.Failed("set");
            logger.LogWarning("Redis set {CacheKey} failed ({FailureType}: {FailureMessage})",
                key, exception.GetType().Name, exception.GetBaseException().Message);
        }
    }
}

/// <summary>Метрики кешу агрегатора (Meter "Platform.Aggregator.Cache"): рівень один — L2 (Redis).</summary>
public sealed class AggregateCacheMetrics
{
    public const string MeterName = "Platform.Aggregator.Cache";

    public static readonly ActivitySource ActivitySource = new(MeterName);

    private static readonly KeyValuePair<string, object?> Level = new("cache.level", "L2");

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _duration;

    public AggregateCacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", "{hit}", "Aggregated responses served from Redis");
        _misses = meter.CreateCounter<long>("cache.misses", "{miss}", "Aggregated responses built via gRPC");
        _errors = meter.CreateCounter<long>("cache.l2.errors", "{error}", "Redis calls that failed");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", "ms", "Duration of Redis calls");
    }

    public void Hit() => _hits.Add(1, Level);

    public void Miss() => _misses.Add(1, Level);

    public void Failed(string operation) => _errors.Add(1, Level, new("operation", operation));

    public void Duration(string operation, double ms) => _duration.Record(ms, Level, new("operation", operation));
}
