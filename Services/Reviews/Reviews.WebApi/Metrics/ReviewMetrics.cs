using System.Diagnostics.Metrics;

namespace Reviews.WebApi.Metrics;

/// <summary>
/// Бізнес-метрики сервісу відгуків (Лаб. 07). Meter "Reviews.WebApi" (ім'я сервісу) додано в ServiceDefaults.
/// Singleton. Записує їх MetricsBehavior — для кожної команди й запиту MediatR, тож контролери й gRPC-сервіс
/// метрик не знають.
///
/// | Інструмент                   | Тип             | Теги (значення)                                       |
/// |------------------------------|-----------------|-------------------------------------------------------|
/// | reviews.created              | Counter         | review.rating (1–5)                                   |
/// | reviews.updated              | Counter         | —                                                     |
/// | reviews.deleted              | Counter         | —                                                     |
/// | reviews.comments.added       | Counter         | author.role (student, instructor)                     |
/// | reviews.operation.duration   | Histogram, мс   | operation (ім'я команди/запиту, ~10 значень), kind (command, query), status (success, failure) |
/// | reviews.operations.in_flight | ObservableGauge | —                                                     |
/// </summary>
public sealed class ReviewMetrics
{
    public const string MeterName = "Reviews.WebApi";

    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;
    private readonly Counter<long> _comments;
    private readonly Histogram<double> _duration;
    private long _inFlight;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>("reviews.created", "{review}", "Reviews created");
        _updated = meter.CreateCounter<long>("reviews.updated", "{review}", "Reviews edited by their authors");
        _deleted = meter.CreateCounter<long>("reviews.deleted", "{review}", "Reviews deleted (soft delete)");
        _comments = meter.CreateCounter<long>("reviews.comments.added", "{comment}", "Comments added to reviews");
        _duration = meter.CreateHistogram<double>("reviews.operation.duration", "ms", "Duration of review commands and queries, successful and failed");

        // Поточний стан, а не накопичене значення: скільки команд і запитів обробляється саме зараз
        meter.CreateObservableGauge("reviews.operations.in_flight", () => Interlocked.Read(ref _inFlight),
            "{operation}", "Review commands and queries being processed right now");
    }

    public void ReviewCreated(int rating) => _created.Add(1, new KeyValuePair<string, object?>("review.rating", rating));

    public void ReviewUpdated() => _updated.Add(1);

    public void ReviewDeleted() => _deleted.Add(1);

    public void CommentAdded(string role) => _comments.Add(1, new KeyValuePair<string, object?>("author.role", role.ToLowerInvariant()));

    public void Started() => Interlocked.Increment(ref _inFlight);

    public void Finished(string operation, bool isCommand, bool success, double elapsedMs)
    {
        Interlocked.Decrement(ref _inFlight);
        _duration.Record(elapsedMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("kind", isCommand ? "command" : "query"),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
    }
}
