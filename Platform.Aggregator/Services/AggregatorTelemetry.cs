using System.Diagnostics;

namespace Platform.Aggregator.Services;

/// <summary>
/// Власні spans агрегації. Ім'я джерела збігається з ApplicationName, тож ServiceDefaults уже на нього
/// підписаний. У trace під span "BuildOrderCard" видно паралельні HTTP-виклики до сервісів.
/// </summary>
public static class AggregatorTelemetry
{
    public static readonly ActivitySource Source = new("Platform.Aggregator");
}
