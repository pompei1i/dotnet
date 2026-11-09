namespace Courses.Bll.Caching;

/// <summary>Розділ "Caching" в appsettings.json.</summary>
public sealed class CachingOptions
{
    public const string SectionName = "Caching";

    /// <summary>
    /// false — кожне читання йде в базу (кеш вимкнено повністю). Потрібно для чесного порівняння
    /// HTTP і gRPC: так різниця в часі агрегації залежить лише від транспорту.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>На скільки призупиняти звернення до Redis після помилки.</summary>
    public TimeSpan L2RetryAfter { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Прогрівати довідник категорій під час старту (CacheWarmupService).</summary>
    public bool WarmupOnStartup { get; set; } = true;

    /// <summary>Скільки опублікованих курсів прогрівати.</summary>
    public int WarmupCourses { get; set; } = 20;

    /// <summary>Як часто оновлювати прогріті дані; 0 — лише під час старту.</summary>
    public TimeSpan WarmupInterval { get; set; } = TimeSpan.FromMinutes(10);
}
