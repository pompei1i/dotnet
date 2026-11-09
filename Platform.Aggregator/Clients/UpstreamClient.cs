using System.Diagnostics;
using System.Net;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Спільна частина typed-клієнтів: GET із логуванням кожного виклику («Calling courses-api ...» ->
/// «courses-api responded 200 in 85 ms») і десеріалізацією JSON у DTO через System.Text.Json.
/// 404 означає «такого запису немає» і повертається як null; інші помилки — винятком, який
/// агрегатор перетворює на попередження. HttpClient приходить з IHttpClientFactory вже з service
/// discovery, resilience і CorrelationIdHandler (ServiceDefaults).
/// </summary>
public abstract class UpstreamClient(HttpClient http, ILogger logger)
{
    /// <summary>Логічне ім'я сервісу в AppHost — воно ж у логах і попередженнях.</summary>
    public abstract string ServiceName { get; }

    protected async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        logger.LogInformation("Calling {Service} GET {UpstreamPath}", ServiceName, path);
        var started = Stopwatch.GetTimestamp();

        using var response = await http.GetAsync(path, ct);

        logger.LogInformation("{Service} responded {UpstreamStatusCode} to GET {UpstreamPath} in {ElapsedMs} ms",
            ServiceName, (int)response.StatusCode, path, ElapsedMs(started));

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }

    private static double ElapsedMs(long started) => Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);
}
