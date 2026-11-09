using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Courses.Bll.Caching;

/// <summary>
/// Дворівневий cache-aside: L1 (IMemoryCache, у пам'яті процесу) -> L2 (IDistributedCache поверх Redis,
/// спільний для всіх екземплярів) -> база даних (factory). Рівні заповнюються на зворотному шляху:
/// промах L1 і попадання L2 кладе значення в L1; промах обох — фабрика, потім L2 і L1.
///
/// Redis — прискорювач, а не залежність: якщо він недоступний, помилка логується, кеш працює лише
/// з L1 і базою, а звернення до Redis на кілька секунд призупиняються, щоб кожен запит не чекав
/// timeout. Сервіс відповідає повільніше, але не падає.
/// </summary>
public sealed class TwoLevelCache(
    IMemoryCache memory,
    IDistributedCache distributed,
    CacheMetrics metrics,
    IOptions<CachingOptions> options,
    ILogger<TwoLevelCache> logger) : ITwoLevelCache
{
    // DTO каталогу — records із конструкторами; System.Text.Json уміє їх і серіалізувати, і відновлювати
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Після помилки Redis наступні звернення до L2 пропускаються до цієї миті (UTC ticks).
    // Поле екземпляра: кеш зареєстровано як singleton, тож пауза одна на процес
    private long _l2PausedUntil;

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CacheEntryPolicy policy, CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return await factory(ct);

        using var activity = CacheMetrics.ActivitySource.StartActivity("cache GetOrCreate");
        activity?.SetTag("cache.key", key);

        // ---- L1: пам'ять процесу ----
        var started = Stopwatch.GetTimestamp();
        if (memory.TryGetValue(key, out T? cached) && cached is not null)
        {
            metrics.Duration("L1", "get", ElapsedMs(started));
            metrics.Hit("L1");
            activity?.SetTag("cache.result", "l1_hit");
            logger.LogDebug("Cache hit {CacheKey} at {CacheLevel}", key, "L1");
            return cached;
        }
        metrics.Miss("L1");

        // ---- L2: Redis ----
        var bytes = await TryGetL2Async(key, ct);
        if (bytes is not null)
        {
            var value = JsonSerializer.Deserialize<T>(bytes, Json);
            if (value is not null)
            {
                metrics.Hit("L2");
                SetL1(key, value, bytes.Length, policy);
                activity?.SetTag("cache.result", "l2_hit");
                logger.LogDebug("Cache hit {CacheKey} at {CacheLevel}, {SizeBytes} bytes, refilled L1 for {L1Ttl}",
                    key, "L2", bytes.Length, policy.L1Absolute);
                return value;
            }
        }
        if (!L2Paused) metrics.Miss("L2");

        // ---- База даних ----
        activity?.SetTag("cache.result", "miss");
        logger.LogInformation("Cache miss {CacheKey}, loading from the database", key);

        var created = await factory(ct);
        if (created is null)
            return created;   // «немає даних» не кешується: наступний запит перевірить базу знову

        var payload = JsonSerializer.SerializeToUtf8Bytes(created, Json);
        await TrySetL2Async(key, payload, policy, ct);
        SetL1(key, created, payload.Length, policy);

        logger.LogInformation("Cache filled {CacheKey}: {SizeBytes} bytes, L1 TTL {L1Ttl}, L2 TTL {L2Ttl}",
            key, payload.Length, policy.L1Absolute, policy.L2Absolute);
        return created;
    }

    public async Task InvalidateAsync(string reason, IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        if (!options.Value.Enabled || keys.Count == 0)
            return;

        using var activity = CacheMetrics.ActivitySource.StartActivity("cache Invalidate");
        activity?.SetTag("cache.keys", string.Join(", ", keys));
        activity?.SetTag("cache.invalidation_reason", reason);

        // Спершу L1 цього екземпляра, потім Redis. Інші екземпляри побачать зміну, щойно сплине їхній
        // короткий TTL L1 (L1 свідомо коротший за L2) — pub/sub для миттєвого оновлення не використовується.
        foreach (var key in keys)
            memory.Remove(key);

        foreach (var key in keys)
            await TryRemoveL2Async(key, ct);

        metrics.Invalidated(reason, keys.Count);
        logger.LogInformation("Cache invalidated {CacheKeys}, reason {InvalidationReason}", keys, reason);
    }

    public async Task<string> GetVersionAsync(string key, CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return "0";

        if (memory.TryGetValue(key, out string? version) && version is not null)
            return version;

        var bytes = await TryGetL2Async(key, ct);
        version = bytes is null ? "0" : System.Text.Encoding.UTF8.GetString(bytes);
        SetL1(key, version, version.Length, CachePolicies.ListVersion);
        return version;
    }

    public async Task BumpVersionAsync(string key, string reason, CancellationToken ct = default)
    {
        if (!options.Value.Enabled)
            return;

        var version = DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await TrySetL2Async(key, System.Text.Encoding.UTF8.GetBytes(version), CachePolicies.ListVersion, ct);
        SetL1(key, version, version.Length, CachePolicies.ListVersion);

        metrics.Invalidated(reason, 1);
        logger.LogInformation("Cache version {CacheKey} bumped to {CacheVersion}, reason {InvalidationReason}",
            key, version, reason);
    }

    private void SetL1<T>(string key, T value, int sizeBytes, CacheEntryPolicy policy)
    {
        var entry = new MemoryCacheEntryOptions
        {
            // SizeLimit L1 задано в KiB: запис займає стільки одиниць, скільки KiB його JSON
            Size = Math.Max(1, sizeBytes / 1024),
            AbsoluteExpirationRelativeToNow = policy.L1Absolute,
            SlidingExpiration = policy.L1Sliding,
            Priority = policy.Priority
        };
        entry.RegisterPostEvictionCallback(static (evictedKey, _, reason, state) =>
        {
            // Replaced/Removed — наша ж робота (перезапис, інвалідація); цікаві витіснення за TTL і місцем
            if (reason is EvictionReason.Expired or EvictionReason.Capacity or EvictionReason.TokenExpired)
                ((CacheMetrics)state!).Evicted(reason);
        }, metrics);

        memory.Set(key, value, entry);
    }

    private bool L2Paused => DateTime.UtcNow.Ticks < Interlocked.Read(ref _l2PausedUntil);

    private async Task<byte[]?> TryGetL2Async(string key, CancellationToken ct)
    {
        if (L2Paused)
            return null;

        var started = Stopwatch.GetTimestamp();
        try
        {
            var bytes = await distributed.GetAsync(key, ct);
            metrics.Duration("L2", "get", ElapsedMs(started));
            return bytes;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            L2Failed("get", key, exception);
            return null;
        }
    }

    private async Task TrySetL2Async(string key, byte[] payload, CacheEntryPolicy policy, CancellationToken ct)
    {
        if (L2Paused)
            return;

        var started = Stopwatch.GetTimestamp();
        try
        {
            await distributed.SetAsync(key, payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy.L2Absolute }, ct);
            metrics.Duration("L2", "set", ElapsedMs(started));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            L2Failed("set", key, exception);
        }
    }

    private async Task TryRemoveL2Async(string key, CancellationToken ct)
    {
        try
        {
            // Інвалідацію не пропускаємо навіть під час паузи: застарілий запис у Redis гірший за зайву спробу
            await distributed.RemoveAsync(key, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            L2Failed("remove", key, exception);
        }
    }

    private void L2Failed(string operation, string key, Exception exception)
    {
        metrics.L2Failed(operation);
        var pause = options.Value.L2RetryAfter;
        Interlocked.Exchange(ref _l2PausedUntil, DateTime.UtcNow.Add(pause).Ticks);
        logger.LogWarning("Redis {CacheOperation} {CacheKey} failed: {FailureType} {FailureMessage}; L2 paused for {L2Pause}, serving from L1 and the database",
            operation, key, exception.GetType().Name, exception.GetBaseException().Message, pause);
    }

    private static double ElapsedMs(long started) => Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
