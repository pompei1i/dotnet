using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.ErrorHandling;

/// <summary>
/// Недоступність обов'язкового сервісу -> 503 Service Unavailable у форматі ProblemDetails
/// (з CorrelationId і TraceId), а не 500. Решту винятків обробляє стандартний обробник (500).
/// </summary>
public sealed class UpstreamUnavailableHandler(IProblemDetailsService problemDetails, ILogger<UpstreamUnavailableHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not UpstreamUnavailableException upstream)
            return false;

        logger.LogWarning("Aggregation aborted: required service {Service} is unavailable", upstream.Service);

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Required service is unavailable",
                Detail = upstream.Message,
                Extensions = { ["service"] = upstream.Service }
            }
        });
    }
}
