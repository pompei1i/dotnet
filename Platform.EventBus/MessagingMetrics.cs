using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Platform.EventBus;

/// <summary>
/// Метрики обробки подій (Meter "Platform.Messaging", додано в ServiceDefaults):
/// messaging.messages.processed (теги queue, event.type, outcome: ack | retry | dead_letter | ignored),
/// messaging.process.duration (мс) і gauge messaging.queue.depth — глибина черги й DLQ.
/// Асинхронні збої «тихі»: клієнта, який побачив би помилку, немає, тож DLQ треба бачити в метриках.
/// </summary>
public sealed class MessagingMetrics
{
    public const string MeterName = TracePropagation.SourceName;

    private readonly Counter<long> _processed;
    private readonly Histogram<double> _duration;
    private readonly ConcurrentDictionary<string, long> _depth = new();

    public MessagingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _processed = meter.CreateCounter<long>("messaging.messages.processed", "{message}", "Messages handled by consumers, by outcome");
        _duration = meter.CreateHistogram<double>("messaging.process.duration", "ms", "Time to handle one message");
        meter.CreateObservableGauge("messaging.queue.depth",
            () => _depth.Select(pair => new Measurement<long>(pair.Value, new KeyValuePair<string, object?>("queue", pair.Key))),
            "{message}", "Messages waiting in the queue (including dead-letter queues)");
    }

    public void Processed(string queue, string eventType, string outcome, double elapsedMs)
    {
        KeyValuePair<string, object?>[] tags =
        [
            new("queue", queue),
            new("event.type", eventType),
            new("outcome", outcome)
        ];
        _processed.Add(1, tags);
        _duration.Record(elapsedMs, tags);
    }

    public void QueueDepth(string queue, long messages) => _depth[queue] = messages;
}
