using Courses.Bll.Caching;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Courses.Api.HealthChecks;

/// <summary>
/// Health checks каталогу (Лаб. 07):
/// - courses-db — SQL Server, SELECT 1, таймаут 5 с, Unhealthy: без бази каталог не працює;
/// - redis-cache — PING через IConnectionMultiplexer кешу, таймаут 3 с, <b>Degraded</b>: кеш необов'язковий,
///   без Redis сервіс читає з бази (graceful degradation, TwoLevelCache) — він працює, лише повільніше;
/// - cache — статистика L1/L2 (Лаб. 06), лише для діагностики на /health, без тегу ready;
/// - gRPC Health Checking Protocol — для Aggregator, на основі перевірок з тегом ready.
/// </summary>
public static class CoursesHealthChecks
{
    public static IHostApplicationBuilder AddCoursesHealthChecks(this IHostApplicationBuilder builder, string connectionString)
    {
        var checks = builder.Services.AddHealthChecks()
            .AddSqlServer(
                // Окремий короткий Connect Timeout: SqlClient не перериває відкриття з'єднання за токеном,
                // тож без цього недоступна база тримала б /health/ready 15 с (таймаут за замовчуванням), а не 5
                connectionString: new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 3 }.ConnectionString,
                healthQuery: "SELECT 1;",
                name: "courses-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", "ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<CacheHealthCheck>("cache-statistics", tags: ["cache"]);

        if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
            checks.AddRedis(
                sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));

        // SERVING / NOT_SERVING для Aggregator — лише за обов'язковими залежностями (база): недоступний
        // Redis не повинен робити каталог «непрацюючим» для інших сервісів
        builder.Services.AddGrpcHealthChecks(options =>
            options.Services.Map("", result => result.Tags.Contains("ready") && !result.Tags.Contains("cache")));

        return builder;
    }
}
