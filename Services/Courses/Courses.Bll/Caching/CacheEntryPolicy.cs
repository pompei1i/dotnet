using Microsoft.Extensions.Caching.Memory;

namespace Courses.Bll.Caching;

/// <summary>
/// Час життя запису на обох рівнях. Правило: L1 живе коротше за L2 — інакше копія в пам'яті одного
/// екземпляра переживе оновлення в Redis, і репліки віддаватимуть різні дані.
/// </summary>
/// <param name="L1Absolute">Абсолютний TTL у пам'яті — верхня межа, навіть якщо запис читають постійно.</param>
/// <param name="L1Sliding">Sliding TTL у пам'яті — запис, який перестали читати, звільняє місце раніше.</param>
/// <param name="L2Absolute">TTL у Redis (спільний для всіх екземплярів).</param>
/// <param name="Priority">Що витісняти першим, коли L1 досягає SizeLimit.</param>
public sealed record CacheEntryPolicy(
    TimeSpan L1Absolute,
    TimeSpan? L1Sliding,
    TimeSpan L2Absolute,
    CacheItemPriority Priority = CacheItemPriority.Normal)
{
    public CacheEntryPolicy Validate()
    {
        if (L1Absolute >= L2Absolute)
            throw new InvalidOperationException($"L1 TTL ({L1Absolute}) must be shorter than L2 TTL ({L2Absolute})");
        if (L1Sliding is { } sliding && sliding > L1Absolute)
            throw new InvalidOperationException($"L1 sliding TTL ({sliding}) must not exceed L1 absolute TTL ({L1Absolute})");
        return this;
    }
}

/// <summary>
/// TTL за типом даних каталогу. Картка курсу читається постійно (sliding у L1), довідник категорій
/// майже не змінюється (лише absolute), сторінки списку залежать від багатьох курсів — найкоротший TTL.
/// Жодних персональних даних каталог не кешує.
/// </summary>
public static class CachePolicies
{
    public static readonly CacheEntryPolicy CourseCard = new CacheEntryPolicy(
        L1Absolute: TimeSpan.FromMinutes(5),
        L1Sliding: TimeSpan.FromMinutes(2),
        L2Absolute: TimeSpan.FromMinutes(30)).Validate();

    public static readonly CacheEntryPolicy Categories = new CacheEntryPolicy(
        L1Absolute: TimeSpan.FromMinutes(10),
        L1Sliding: null,
        L2Absolute: TimeSpan.FromHours(1),
        CacheItemPriority.High).Validate();

    public static readonly CacheEntryPolicy CourseListPage = new CacheEntryPolicy(
        L1Absolute: TimeSpan.FromMinutes(1),
        L1Sliding: null,
        L2Absolute: TimeSpan.FromMinutes(5),
        CacheItemPriority.Low).Validate();

    /// <summary>Ключ-версія списків: у пам'яті кілька секунд, щоб інші репліки швидко побачили нову версію.</summary>
    public static readonly CacheEntryPolicy ListVersion = new CacheEntryPolicy(
        L1Absolute: TimeSpan.FromSeconds(5),
        L1Sliding: null,
        L2Absolute: TimeSpan.FromDays(1),
        CacheItemPriority.High).Validate();
}
