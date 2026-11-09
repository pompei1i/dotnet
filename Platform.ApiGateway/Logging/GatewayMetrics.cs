using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Platform.ApiGateway.Logging;

/// <summary>
/// Власні метрики Gateway (Meter "Platform.ApiGateway" — ServiceDefaults підписує на нього OpenTelemetry).
/// У вкладці Metrics Dashboard з них видно кількість запитів за секунду по маршрутах, середній час
/// відповіді та частку помилок. Стандартна http.server.request.duration від ASP.NET Core теж є.
/// </summary>
public sealed class GatewayMetrics
{
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _proxyErrors;

    public GatewayMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Platform.ApiGateway");

        _requests = meter.CreateCounter<long>("gateway.requests", unit: "{request}",
            description: "Requests that passed through the gateway");
        _duration = meter.CreateHistogram<double>("gateway.request.duration", unit: "ms",
            description: "Gateway request duration including the downstream service");
        _proxyErrors = meter.CreateCounter<long>("gateway.proxy.errors", unit: "{error}",
            description: "Proxy errors: service unavailable, timeout, connection refused");
    }

    public void RecordRequest(string routeId, string? clusterId, int statusCode, double elapsedMs, string? proxyError)
    {
        var tags = new TagList
        {
            { "route", routeId },
            { "cluster", clusterId ?? "none" },
            { "status_class", $"{statusCode / 100}xx" }
        };

        _requests.Add(1, tags);
        _duration.Record(elapsedMs, tags);

        if (proxyError is not null)
            _proxyErrors.Add(1, new TagList { { "route", routeId }, { "error", proxyError } });
    }
}
