using Microsoft.AspNetCore.Http;

namespace Platform.ServiceDefaults;

/// <summary>
/// DelegatingHandler для всіх HttpClient (підключено в ConfigureHttpClientDefaults): додає у вихідний
/// запит той самий X-Correlation-Id, з яким прийшов поточний вхідний запит. Так ідентифікатор
/// проходить далі, наприклад Aggregator -> orders-api / catalog-api / reviews-api.
/// </summary>
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();

        if (!string.IsNullOrEmpty(correlationId) && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
