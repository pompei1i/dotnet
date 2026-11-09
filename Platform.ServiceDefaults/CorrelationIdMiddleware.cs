using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults;

/// <summary>
/// Наскрізний CorrelationId — бізнес-ідентифікатор звернення, один на весь ланцюжок викликів.
/// Береться із заголовка X-Correlation-Id або генерується (GUID) — зазвичай у Gateway, а в сервісах
/// лише тоді, коли до них звернулися в обхід Gateway. Записується назад у Request.Headers, звідки
/// його бачать YARP (копіює заголовки в проксований запит) і CorrelationIdHandler (вихідні HttpClient).
/// На відміну від TraceId, який генерує OpenTelemetry для технічного трасування, CorrelationId
/// створює й передає сам застосунок; його можна назвати підтримці з заголовка відповіді.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogPropertyName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        // Заголовок приходить від клієнта, тому приймаємо лише безпечне значення
        var incoming = context.Request.Headers[HeaderName].ToString();
        var isGenerated = !SafeId().IsMatch(incoming);
        var correlationId = isGenerated ? Guid.NewGuid().ToString() : incoming;

        context.Request.Headers[HeaderName] = correlationId;
        context.Items[HeaderName] = correlationId;

        // У відповідь — через OnStarting: YARP копіює заголовки відповіді сервісу, і без цього
        // клієнт отримав би значення двічі ("id, id"). Тут воно встановлюється останнім і один раз.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Зв'язок двох ідентифікаторів: у span запиту видно CorrelationId, а в логах — TraceId
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        {
            // Перевірки здоров'я Aspire опитує кожні кілька секунд — межі таких запитів не логуємо
            if (context.Request.Path.StartsWithSegments("/health") || context.Request.Path.StartsWithSegments("/alive"))
            {
                await next(context);
                return;
            }

            if (isGenerated)
                logger.LogInformation("CorrelationId {CorrelationId} generated for {Method} {Path}",
                    correlationId, context.Request.Method, context.Request.Path.Value);

            // Boundary logging: межі запиту в кожному сервісі ланцюжка
            var started = Stopwatch.GetTimestamp();
            logger.LogInformation("Request started {Method} {Path}", context.Request.Method, context.Request.Path.Value);
            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation("Request finished {Method} {Path} with {StatusCode} in {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path.Value, context.Response.StatusCode,
                    Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1));
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeId();
}
