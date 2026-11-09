using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;

namespace Courses.Bll.Caching;

/// <summary>
/// Метрики ефективності кешу (Meter "Courses.Cache"). ServiceDefaults підписує OpenTelemetry на
/// метри "*.Cache", тож вони з'являються у вкладці Metrics Aspire Dashboard:
/// cache.hits / cache.misses з тегом cache.level (L1 | L2) — з них рахується hit ratio;
/// cache.hit_ratio — уже порахована частка попадань кожного рівня;
/// cache.invalidations (тег reason), cache.evictions (тег reason: Expired, Capacity...);
/// cache.operation.duration — тривалість звернень до рівнів; cache.l1.entries / cache.l1.size — заповненість L1.
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Courses.Cache";

    /// <summary>Spans операцій кешу; ім'я підпадає під "Courses.*", на яке підписаний ServiceDefaults.</summary>
    public static readonly ActivitySource ActivitySource = new(MeterName);

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Counter<long> _evictions;
    private readonly Counter<long> _l2Errors;
    private readonly Histogram<double> _duration;

    // Для hit ratio: лічильники в пам'яті процесу (OpenTelemetry-лічильники назад не читаються)
    private long _l1Hits, _l1Misses, _l2Hits, _l2Misses;

    public CacheMetrics(IMeterFactory meterFactory, IMemoryCache memoryCache)
    {
        var meter = meterFactory.Create(MeterName);

        _hits = meter.CreateCounter<long>("cache.hits", "{hit}", "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", "{miss}", "Cache misses by level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", "{key}", "Keys removed after writes");
        _evictions = meter.CreateCounter<long>("cache.evictions", "{entry}", "L1 entries evicted by the memory cache");
        _l2Errors = meter.CreateCounter<long>("cache.l2.errors", "{error}", "Redis calls that failed (served from the database)");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", "ms", "Duration of cache level lookups");

        meter.CreateObservableGauge("cache.hit_ratio", () => new[]
        {
            new Measurement<double>(Ratio(_l1Hits, _l1Misses), Level("L1")),
            new Measurement<double>(Ratio(_l2Hits, _l2Misses), Level("L2"))
        }, description: "Share of lookups served by the level");

        // Статистику IMemoryCache дає TrackStatistics = true (Program.cs)
        meter.CreateObservableGauge("cache.l1.entries",
            () => memoryCache.GetCurrentStatistics()?.CurrentEntryCount ?? 0, "{entry}", "Entries in the L1 memory cache");
        meter.CreateObservableGauge("cache.l1.size",
            () => memoryCache.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0, "KiBy", "Estimated L1 size (sum of entry sizes, KiB)");
    }

    public void Hit(string level)
    {
        _hits.Add(1, Level(level));
        if (level == "L1") Interlocked.Increment(ref _l1Hits); else Interlocked.Increment(ref _l2Hits);
    }

    public void Miss(string level)
    {
        _misses.Add(1, Level(level));
        if (level == "L1") Interlocked.Increment(ref _l1Misses); else Interlocked.Increment(ref _l2Misses);
    }

    public void Invalidated(string reason, int keys) => _invalidations.Add(keys, new KeyValuePair<string, object?>("reason", reason));

    public void Evicted(EvictionReason reason) => _evictions.Add(1, new KeyValuePair<string, object?>("reason", reason.ToString()));

    public void L2Failed(string operation) => _l2Errors.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void Duration(string level, string operation, double ms)
        => _duration.Record(ms, Level(level), new KeyValuePair<string, object?>("operation", operation));

    /// <summary>Знімок для health check: частка попадань кожного рівня.</summary>
    public (double L1, double L2) HitRatios => (Ratio(_l1Hits, _l1Misses), Ratio(_l2Hits, _l2Misses));

    private static KeyValuePair<string, object?> Level(string level) => new("cache.level", level);

    private static double Ratio(in long hitsField, in long missesField)
    {
        var hits = Volatile.Read(in hitsField);
        var total = hits + Volatile.Read(in missesField);
        return total == 0 ? 0 : Math.Round((double)hits / total, 4);
    }
}
