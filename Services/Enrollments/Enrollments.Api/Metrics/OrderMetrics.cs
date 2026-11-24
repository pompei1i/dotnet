using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Enrollments.Api.Metrics;

/// <summary>
/// Бізнес-метрики сервісу замовлень (Лаб. 07). Meter "Enrollments.Api" — ім'я сервісу; ServiceDefaults
/// додає його в OpenTelemetry (BusinessMeters), Dashboard показує інструменти у вкладці Metrics.
/// Реєструється як Singleton: інструменти живуть увесь час роботи застосунку.
///
/// | Інструмент                  | Тип            | Теги (значення)                                                    |
/// |-----------------------------|----------------|--------------------------------------------------------------------|
/// | orders.created              | Counter        | payment.method (Card, PayPal, BankTransfer)                        |
/// | orders.amount               | Counter&lt;double&gt; | payment.method                                                 |
/// | orders.status_changed       | Counter        | order.status — новий статус (6 значень OrderStatuses)              |
/// | orders.items.added          | Counter        | —                                                                  |
/// | orders.operation.duration   | Histogram, мс  | operation (create, add_item, change_status, delete, get), status (success, failure) |
/// | orders.by_status            | ObservableGauge| order.status                                                       |
///
/// Кардинальність низька: лише перелічені значення. Id замовлення, студента чи курсу в тегах
/// немає — кожна унікальна комбінація тегів стала б окремим часовим рядом.
/// </summary>
public sealed class OrderMetrics
{
    public const string MeterName = "Enrollments.Api";

    private readonly Counter<long> _created;
    private readonly Counter<double> _amount;
    private readonly Counter<long> _statusChanged;
    private readonly Counter<long> _itemsAdded;
    private readonly Histogram<double> _duration;

    // Знімок для gauge оновлює OrderStatusSnapshotService; callback лише читає поле — без запитів до бази
    private volatile IReadOnlyDictionary<string, int> _byStatus = new Dictionary<string, int>();

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>("orders.created", "{order}", "Orders created");
        _amount = meter.CreateCounter<double>("orders.amount", "UAH", "Total amount of created orders");
        _statusChanged = meter.CreateCounter<long>("orders.status_changed", "{order}", "Order status transitions by new status");
        _itemsAdded = meter.CreateCounter<long>("orders.items.added", "{item}", "Courses added to existing orders");
        _duration = meter.CreateHistogram<double>("orders.operation.duration", "ms", "Duration of order operations, successful and failed");

        meter.CreateObservableGauge("orders.by_status",
            () => _byStatus.Select(pair => new Measurement<int>(pair.Value, new KeyValuePair<string, object?>("order.status", pair.Key))),
            "{order}", "Orders currently in each status");
    }

    public void OrderCreated(string paymentMethod, decimal amount)
    {
        var tag = new KeyValuePair<string, object?>("payment.method", paymentMethod);
        _created.Add(1, tag);
        _amount.Add((double)amount, tag);
    }

    public void StatusChanged(string newStatus) => _statusChanged.Add(1, new KeyValuePair<string, object?>("order.status", newStatus));

    public void ItemAdded() => _itemsAdded.Add(1);

    public void UpdateStatusSnapshot(IReadOnlyDictionary<string, int> byStatus) => _byStatus = byStatus;

    /// <summary>
    /// Виконує операцію й записує її тривалість — і для успішних, і для невдалих викликів
    /// (інакше розподіл затримок показував би лише «добрі» запити).
    /// </summary>
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await action();
            Record(operation, success: true, started);
            return result;
        }
        catch
        {
            Record(operation, success: false, started);
            throw;
        }
    }

    public Task MeasureAsync(string operation, Func<Task> action)
        => MeasureAsync(operation, async () => { await action(); return true; });

    private void Record(string operation, bool success, long started)
        => _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
}
