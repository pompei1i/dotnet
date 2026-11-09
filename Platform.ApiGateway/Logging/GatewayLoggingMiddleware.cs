using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Platform.ApiGateway.Logging;

/// <summary>
/// Логує кожен запит, що пройшов через Gateway, одним структурованим записом: метод, шлях, маршрут
/// і кластер YARP, адресу екземпляра сервісу, статус, тривалість, розмір відповіді та IP клієнта.
/// Помилки проксування (сервіс недоступний, timeout, connection refused) — окремим записом рівня Error
/// з типом помилки YARP і винятком. Ті самі показники йдуть у метрики (GatewayMetrics).
/// </summary>
public sealed class GatewayLoggingMiddleware(
    RequestDelegate next,
    ILogger<GatewayLoggingMiddleware> logger,
    GatewayMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Перевірки здоров'я Aspire опитує постійно — вони лише засмічували б лог
        if (context.Request.Path.StartsWithSegments("/health") || context.Request.Path.StartsWithSegments("/alive"))
        {
            await next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();

        // Розмір відповіді рахується на льоту: відповіді сервісів зазвичай chunked, без Content-Length
        var originalBody = context.Response.Body;
        var countingBody = new CountingStream(originalBody);
        context.Response.Body = countingBody;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;

            var elapsedMs = Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);
            var proxy = context.Features.Get<IReverseProxyFeature>();
            var routeId = proxy?.Route.Config.RouteId ?? "unmatched";
            var clusterId = proxy?.Cluster?.Config.ClusterId;
            var destination = proxy?.ProxiedDestination?.Model.Config.Address;
            var statusCode = context.Response.StatusCode;
            var path = context.Request.Path.Value + context.Request.QueryString.Value;

            var error = context.Features.Get<IForwarderErrorFeature>();
            if (error is not null)
            {
                logger.LogError(error.Exception,
                    "Proxy error {ProxyError} for {Method} {Path} -> route {RouteId}, cluster {ClusterId}, destination {Destination}: {ErrorMessage}",
                    error.Error, context.Request.Method, path, routeId, clusterId, destination,
                    error.Exception?.GetBaseException().Message);
            }

            logger.Log(statusCode >= 500 ? LogLevel.Warning : LogLevel.Information,
                "{Method} {Path} -> route {RouteId}, cluster {ClusterId} ({Destination}): {StatusCode} in {ElapsedMs} ms, {ResponseBytes} bytes, client {ClientIp}",
                context.Request.Method, path,
                routeId, clusterId, destination, statusCode, elapsedMs, countingBody.BytesWritten,
                context.Connection.RemoteIpAddress?.ToString());

            metrics.RecordRequest(routeId, clusterId, statusCode, elapsedMs, error?.Error.ToString());
        }
    }
}
