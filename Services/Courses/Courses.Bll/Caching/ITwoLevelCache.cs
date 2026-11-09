namespace Courses.Bll.Caching;

/// <summary>Дворівневий кеш каталогу (L1 пам'ять -> L2 Redis -> база); реалізація — TwoLevelCache.</summary>
public interface ITwoLevelCache
{
    /// <summary>Cache-aside: значення з L1, інакше з L2, інакше з factory (база) — і запис у обидва рівні.</summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CacheEntryPolicy policy,
        CancellationToken ct = default);

    /// <summary>
    /// Видаляє ключі з обох рівнів одразу після запису в базу. Викликається синхронно з операцією
    /// (await до відповіді клієнту) — інакше наступний GET міг би повернути застарілі дані.
    /// </summary>
    Task InvalidateAsync(string reason, IReadOnlyCollection<string> keys, CancellationToken ct = default);

    /// <summary>Поточна версія групи ключів (наприклад, усіх сторінок списку).</summary>
    Task<string> GetVersionAsync(string key, CancellationToken ct = default);

    /// <summary>Масова інвалідація: нова версія робить усі старі ключі групи недосяжними.</summary>
    Task BumpVersionAsync(string key, string reason, CancellationToken ct = default);
}
