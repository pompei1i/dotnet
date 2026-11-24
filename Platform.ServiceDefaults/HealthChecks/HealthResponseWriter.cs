using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Platform.ServiceDefaults.HealthChecks;

/// <summary>
/// JSON-відповідь health endpoints з деталями кожної перевірки (Лаб. 07):
/// <code>
/// { "status": "Degraded", "totalDurationMs": 12.4,
///   "checks": [ { "name": "courses-db", "status": "Healthy", "durationMs": 3.1, "tags": ["database","sql","ready"] },
///               { "name": "redis-cache", "status": "Degraded", "description": "...", "error": "RedisConnectionException" } ] }
/// </code>
/// HTTP-статус задає сам middleware: Healthy і Degraded -> 200, Unhealthy -> 503.
/// Текст винятку може містити адресу сервера, тож назовні він виходить лише в Development;
/// в інших середовищах — тільки тип винятку.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var showErrors = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();

        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                tags = entry.Value.Tags,
                error = entry.Value.Exception is { } exception
                    ? showErrors ? $"{exception.GetType().Name}: {exception.Message}" : exception.GetType().Name
                    : null,
                data = entry.Value.Data.Count > 0 ? entry.Value.Data : null
            })
        };

        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
