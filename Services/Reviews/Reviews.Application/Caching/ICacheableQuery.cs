namespace Reviews.Application.Caching;

/// <summary>
/// Запит, результат якого можна кешувати. CachingBehavior бере з нього ключ, теги й TTL;
/// CacheKey == null — саме цей екземпляр запиту не кешується (наприклад, повнотекстовий пошук
/// чи відгуки конкретного студента — необмежена кількість варіантів і персональні дані).
/// </summary>
public interface ICacheableQuery
{
    string? CacheKey { get; }

    /// <summary>Теги для групової інвалідації: "reviews:course:{id}" — усе, що залежить від відгуків курсу.</summary>
    IReadOnlyCollection<string> CacheTags { get; }

    ReviewsCachePolicy CachePolicy { get; }
}

/// <param name="Local">TTL у пам'яті процесу (L1) — коротший.</param>
/// <param name="Distributed">TTL у Redis (L2), спільний для всіх екземплярів.</param>
public sealed record ReviewsCachePolicy(TimeSpan Local, TimeSpan Distributed)
{
    /// <summary>Рейтинг курсу: змінюється лише з новим/зміненим/видаленим відгуком, а його інвалідація явна.</summary>
    public static readonly ReviewsCachePolicy Rating = new(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));

    /// <summary>Сторінка відгуків курсу: кількість коментарів змінюється частіше — коротший TTL.</summary>
    public static readonly ReviewsCachePolicy ReviewsPage = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
}
