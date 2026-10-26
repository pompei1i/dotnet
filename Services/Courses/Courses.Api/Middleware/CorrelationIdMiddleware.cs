using System.Text.RegularExpressions;
using Serilog.Context;

namespace Courses.Api.Middleware;

/// <summary>
/// Кореляційний ідентифікатор запиту: береться із заголовка X-Correlation-ID або генерується,
/// повертається клієнту в тому самому заголовку й додається як властивості CorrelationId
/// і RequestPath до кожного запису логу протягом запиту.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        // Заголовок приходить від клієнта, тому в лог потрапляє лише безпечне значення
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeId().IsMatch(incoming) ? incoming : context.TraceIdentifier;

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeId();
}
