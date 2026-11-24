using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Aggregator.Resilience;
using StackExchange.Redis;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Readiness Aggregator (Лаб. 07): чи доступні сервіси, з яких він збирає відповіді, і кеш.
/// Усі перевірки мають тег "ready" і видно їх на /health/ready з JSON-деталями.
/// </summary>
public static class AggregatorHealthChecks
{
    public static IHostApplicationBuilder AddAggregatorHealthChecks(this IHostApplicationBuilder builder)
    {
        var checks = builder.Services.AddHealthChecks();

        foreach (var (policy, address) in new[]
                 {
                     (ServiceResiliencePolicy.Enrollments, "http://_grpc.enrollments-api"),
                     (ServiceResiliencePolicy.Courses, "http://_grpc.courses-api"),
                     (ServiceResiliencePolicy.Reviews, "http://_grpc.reviews-api")
                 })
        {
            // Іменований клієнт стандартного gRPC Health-сервісу (Grpc.HealthCheck) для кожної залежності
            builder.Services.AddGrpcClient<Health.HealthClient>(policy.Service, o => o.Address = new Uri(address))
                .RemoveAllResilienceHandlers();

            checks.AddTypeActivatedCheck<GrpcDependencyHealthCheck>(
                name: $"grpc-{policy.Service}",
                failureStatus: policy.Critical ? HealthStatus.Unhealthy : HealthStatus.Degraded,
                tags: ["grpc", "downstream", "ready"],
                timeout: GrpcDependencyHealthCheck.Deadline + TimeSpan.FromSeconds(1),
                args: policy.Service);
        }

        // Redis — кеш агрегованих відповідей, необов'язковий: без нього Aggregator збирає відповідь через gRPC.
        // Тому Degraded, а не Unhealthy. PING іде через той самий IConnectionMultiplexer, що й кеш
        if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
            checks.AddRedis(
                sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));

        return builder;
    }
}
