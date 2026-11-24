using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Courses.Api.Metrics;

/// <summary>
/// Бізнес-метрики каталогу (Лаб. 07). Meter "Courses.Api" (ім'я сервісу) додано в ServiceDefaults.
/// Singleton на весь час роботи застосунку. Метрики кешу каталогу — окремий Meter "Courses.Cache" (Лаб. 06).
///
/// | Інструмент                   | Тип             | Теги (значення)                                         |
/// |------------------------------|-----------------|---------------------------------------------------------|
/// | courses.created              | Counter         | course.level (Beginner, Intermediate, Advanced)         |
/// | courses.price_changed        | Counter         | —                                                       |
/// | courses.publication_changed  | Counter         | action (publish, unpublish)                             |
/// | courses.lessons.added        | Counter         | —                                                       |
/// | courses.deleted              | Counter         | —                                                       |
/// | courses.operation.duration   | Histogram, мс   | operation (create, change_price, publish, unpublish, add_lesson, assign_category, delete, get), status (success, failure) |
/// | courses.catalog.size         | ObservableGauge | course.state (published, draft)                         |
///
/// Ні Id курсу, ні slug, ні назва в теги не потрапляють — лише короткі переліки значень.
/// </summary>
public sealed class CatalogMetrics
{
    public const string MeterName = "Courses.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _priceChanged;
    private readonly Counter<long> _publication;
    private readonly Counter<long> _lessonsAdded;
    private readonly Counter<long> _deleted;
    private readonly Histogram<double> _duration;

    private volatile CatalogSnapshot _snapshot = new(0, 0);

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>("courses.created", "{course}", "Courses created");
        _priceChanged = meter.CreateCounter<long>("courses.price_changed", "{course}", "Course price changes");
        _publication = meter.CreateCounter<long>("courses.publication_changed", "{course}", "Courses published or unpublished");
        _lessonsAdded = meter.CreateCounter<long>("courses.lessons.added", "{lesson}", "Lessons added to courses");
        _deleted = meter.CreateCounter<long>("courses.deleted", "{course}", "Courses deleted (soft delete)");
        _duration = meter.CreateHistogram<double>("courses.operation.duration", "ms", "Duration of catalog operations, successful and failed");

        meter.CreateObservableGauge("courses.catalog.size", () => new[]
        {
            new Measurement<int>(_snapshot.Published, new KeyValuePair<string, object?>("course.state", "published")),
            new Measurement<int>(_snapshot.Draft, new KeyValuePair<string, object?>("course.state", "draft"))
        }, "{course}", "Courses in the catalog by state");
    }

    public void CourseCreated(string level) => _created.Add(1, new KeyValuePair<string, object?>("course.level", level));

    public void PriceChanged() => _priceChanged.Add(1);

    public void PublicationChanged(bool published)
        => _publication.Add(1, new KeyValuePair<string, object?>("action", published ? "publish" : "unpublish"));

    public void LessonAdded() => _lessonsAdded.Add(1);

    public void CourseDeleted() => _deleted.Add(1);

    public void UpdateSnapshot(int published, int draft) => _snapshot = new CatalogSnapshot(published, draft);

    /// <summary>Виконує операцію й записує тривалість — для успішних і невдалих викликів.</summary>
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

    private sealed record CatalogSnapshot(int Published, int Draft);
}
