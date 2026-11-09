using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Reviews.Application.Caching;

/// <summary>
/// Метрики кешу відгуків (Meter "Reviews.Cache"; ServiceDefaults підписаний на "*.Cache").
/// HybridCache не повідомляє, з якого рівня прийшло значення, тож рівень тут один — "hybrid" (L1 + L2):
/// промах — це виклик фабрики (запит до MongoDB), попадання — усе інше.
/// </summary>
public sealed class ReviewsCacheMetrics
{
    public const string MeterName = "Reviews.Cache";
    public const string Level = "hybrid";

    public static readonly ActivitySource ActivitySource = new(MeterName);

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _duration;

    public ReviewsCacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", "{hit}", "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", "{miss}", "Cache misses by level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", "{tag}", "Tags invalidated after writes");
        _errors = meter.CreateCounter<long>("cache.l2.errors", "{error}", "Cache calls that failed (served from MongoDB)");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", "ms", "Duration of cached query lookups");
    }

    public void Hit(string query) => _hits.Add(1, LevelTag, new("query", query));

    public void Miss(string query) => _misses.Add(1, LevelTag, new("query", query));

    public void Invalidated(string reason) => _invalidations.Add(1, new KeyValuePair<string, object?>("reason", reason));

    /// <summary>Звернення до кешу впало (Redis недоступний) — результат узято з бази.</summary>
    public void Failed(string operation) => _errors.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void Duration(bool hit, double ms) => _duration.Record(ms, LevelTag, new("cache.result", hit ? "hit" : "miss"));

    private static readonly KeyValuePair<string, object?> LevelTag = new("cache.level", Level);
}
