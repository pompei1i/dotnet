namespace Reviews.Application.Common.Interfaces;

/// <summary>
/// Оновлення денормалізованих даних Каталогу в feedback_db за інтеграційними подіями (Лаб. 08).
/// Реалізація — Infrastructure (MongoDB); обробники Application про колекції не знають.
/// </summary>
public interface ICatalogSyncStore
{
    /// <summary>Чи вже оброблено подію з таким EventId (колекція processed_events, унікальний _id).</summary>
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct);

    /// <summary>
    /// Фіксує нову назву курсу в read model catalog_courses, лише якщо подія не старша за вже застосовану
    /// (захист від out-of-order). false — подія застаріла, відгуки оновлювати не треба.
    /// </summary>
    Task<bool> TryAdvanceCourseTitleAsync(long courseId, string title, DateTime occurredAt, CancellationToken ct);

    /// <summary>courseTitle в усіх відгуках і обговореннях курсу. Ідемпотентно: повтор дає той самий стан.</summary>
    Task<(long Reviews, long Discussions)> UpdateCourseTitleAsync(long courseId, string title, CancellationToken ct);

    /// <summary>Запам'ятовує EventId як оброблений (повторна вставка того самого Id ігнорується).</summary>
    Task MarkProcessedAsync(Guid eventId, string eventType, string consumer, DateTime occurredAt, CancellationToken ct);
}
