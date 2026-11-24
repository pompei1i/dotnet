using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Reviews.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Reviews.WebApi.HealthChecks;

/// <summary>
/// Health checks сервісу відгуків (Лаб. 07):
/// - reviews-db — MongoDB, команда ping через той самий IMongoClient, що й репозиторії; таймаут 5 с, Unhealthy;
/// - redis-cache — PING Redis (L2 HybridCache), таймаут 3 с, <b>Degraded</b>: без Redis запити йдуть у MongoDB;
/// - gRPC Health Checking Protocol — для Aggregator, лише за обов'язковою залежністю (MongoDB).
/// </summary>
public static class ReviewsHealthChecks
{
    public static IHostApplicationBuilder AddReviewsHealthChecks(this IHostApplicationBuilder builder)
    {
        var checks = builder.Services.AddHealthChecks()
            .AddMongoDb(
                clientFactory: sp => sp.GetRequiredService<MongoDbContext>().Client,
                databaseNameFactory: sp => sp.GetRequiredService<IOptions<MongoDbOptions>>().Value.DatabaseName,
                name: "reviews-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "mongodb", "ready"],
                timeout: TimeSpan.FromSeconds(5));

        if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
            checks.AddRedis(
                sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));

        builder.Services.AddGrpcHealthChecks(options =>
            options.Services.Map("", result => result.Tags.Contains("ready") && !result.Tags.Contains("cache")));

        return builder;
    }
}
