using Enrollments.Bll.Services;

namespace Enrollments.Api.Metrics;

/// <summary>
/// Оновлює знімок для gauge orders.by_status раз на 15 с. Callback ObservableGauge викликається
/// при кожному зборі метрик (в Aspire — щосекунди), тож читати базу прямо в ньому не можна;
/// він бере готове значення з OrderMetrics. Помилка бази лише логується — метрики не важливіші за сервіс.
/// </summary>
public sealed class OrderStatusSnapshotService(
    IServiceScopeFactory scopes,
    OrderMetrics metrics,
    ILogger<OrderStatusSnapshotService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                metrics.UpdateStatusSnapshot(await orders.GetCountsByStatusAsync(stoppingToken));
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Orders snapshot for metrics failed: {FailureType} {FailureMessage}",
                    exception.GetType().Name, exception.GetBaseException().Message);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
