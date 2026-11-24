using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Enrollments.Api.HealthChecks;

/// <summary>
/// Health checks сервісу замовлень (Лаб. 07).
/// - enrollments-db — SQL Server: найлегший запит SELECT 1 з таймаутом 5 с; без бази сервіс нічого
///   не обслужить, тож Unhealthy. Теги database, sql, ready — перевірка входить у /health/ready, а не в /health/live.
/// - gRPC Health Checking Protocol (grpc.health.v1.Health) — його опитує Aggregator. Відповідь будується
///   з тих самих перевірок з тегом ready: SERVING, поки база доступна.
/// </summary>
public static class EnrollmentsHealthChecks
{
    public static IHostApplicationBuilder AddEnrollmentsHealthChecks(this IHostApplicationBuilder builder, string connectionString)
    {
        builder.Services.AddHealthChecks()
            .AddSqlServer(
                // Окремий короткий Connect Timeout: SqlClient не перериває відкриття з'єднання за токеном,
                // тож без цього недоступна база тримала б /health/ready 15 с (таймаут за замовчуванням), а не 5
                connectionString: new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 3 }.ConnectionString,
                healthQuery: "SELECT 1;",
                name: "enrollments-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", "ready"],
                timeout: TimeSpan.FromSeconds(5));

        builder.Services.AddGrpcHealthChecks(options =>
            options.Services.Map("", result => result.Tags.Contains("ready")));

        return builder;
    }
}
