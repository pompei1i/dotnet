namespace Platform.Aggregator.Services;

/// <summary>Недоступний сервіс, без даних якого відповідь не має сенсу (наприклад, саме замовлення).</summary>
public sealed class UpstreamUnavailableException(string service, string reason, Exception innerException)
    : Exception($"{service} is unavailable: {reason}", innerException)
{
    public string Service { get; } = service;
}
