using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Courses.Bll.Caching;

/// <summary>
/// Статистика кешу в /health: кількість записів і розмір L1, частка попадань L1 і L2.
/// Завжди Healthy — порожній чи холодний кеш не є несправністю (доступність Redis перевіряє
/// окремий health check Aspire-інтеграції).
/// </summary>
public sealed class CacheHealthCheck(IMemoryCache memory, CacheMetrics metrics) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var statistics = memory.GetCurrentStatistics();
        var (l1, l2) = metrics.HitRatios;

        var data = new Dictionary<string, object>
        {
            ["l1.entries"] = statistics?.CurrentEntryCount ?? 0,
            ["l1.sizeKiB"] = statistics?.CurrentEstimatedSize ?? 0,
            ["l1.totalHits"] = statistics?.TotalHits ?? 0,
            ["l1.totalMisses"] = statistics?.TotalMisses ?? 0,
            ["l1.hitRatio"] = l1,
            ["l2.hitRatio"] = l2
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            $"L1 {data["l1.entries"]} entries / {data["l1.sizeKiB"]} KiB, hit ratio L1 {l1:P0}, L2 {l2:P0}", data));
    }
}
