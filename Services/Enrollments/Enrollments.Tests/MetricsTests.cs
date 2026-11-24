using System.Diagnostics.Metrics;
using Enrollments.Api.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace Enrollments.Tests;

/// <summary>Бізнес-метрики сервісу замовлень (Лаб. 07): що, з якими тегами і коли записується.</summary>
public class MetricsTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly MeterListener _listener = new();
    private readonly List<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measurements = [];

    public MetricsTests()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OrderMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((i, v, tags, _) => Add(i, v, tags));
        _listener.SetMeasurementEventCallback<double>((i, v, tags, _) => Add(i, v, tags));
        _listener.SetMeasurementEventCallback<int>((i, v, tags, _) => Add(i, v, tags));
        _listener.Start();
    }

    private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        lock (_measurements)
            _measurements.Add((instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
    }

    private OrderMetrics CreateMetrics() => new(_services.GetRequiredService<IMeterFactory>());

    [Fact]
    public void Created_order_increments_counter_and_amount_with_payment_method_tag()
    {
        var metrics = CreateMetrics();

        metrics.OrderCreated("Card", 1500m);

        Assert.Contains(_measurements, m => m.Instrument == "orders.created" && m.Value == 1 && (string?)m.Tags["payment.method"] == "Card");
        Assert.Contains(_measurements, m => m.Instrument == "orders.amount" && m.Value == 1500);
    }

    [Fact]
    public async Task Duration_is_recorded_for_failed_operations_too()
    {
        var metrics = CreateMetrics();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            metrics.MeasureAsync("create", () => Task.FromException(new InvalidOperationException())));
        await metrics.MeasureAsync("get", () => Task.CompletedTask);

        Assert.Contains(_measurements, m => m.Instrument == "orders.operation.duration"
                                            && (string?)m.Tags["operation"] == "create" && (string?)m.Tags["status"] == "failure");
        Assert.Contains(_measurements, m => m.Instrument == "orders.operation.duration"
                                            && (string?)m.Tags["operation"] == "get" && (string?)m.Tags["status"] == "success");
    }

    [Fact]
    public void Gauge_reports_snapshot_per_status_without_ids()
    {
        var metrics = CreateMetrics();
        metrics.UpdateStatusSnapshot(new Dictionary<string, int> { ["Paid"] = 3, ["Submitted"] = 1 });

        _listener.RecordObservableInstruments();

        var gauge = _measurements.Where(m => m.Instrument == "orders.by_status").ToList();
        Assert.Contains(gauge, m => (string?)m.Tags["order.status"] == "Paid" && m.Value == 3);
        Assert.All(gauge, m => Assert.Equal(["order.status"], m.Tags.Keys));   // жодних Id у тегах
    }

    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }
}
