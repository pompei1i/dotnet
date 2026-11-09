namespace Reviews.Application.Caching;

/// <summary>
/// Схема ключів сервісу відгуків: префікс "reviews:" ізолює їх від ключів каталогу й агрегатора
/// у спільному Redis. Тег курсу об'єднує всі записи, що залежать від його відгуків, — рейтинг і
/// сторінки списку; один RemoveByTagAsync після запису видаляє їх разом.
/// </summary>
public static class ReviewsCacheKeys
{
    public const string Prefix = "reviews:";

    public static string Rating(long courseId) => $"{Prefix}rating:{courseId}";

    public static string CourseReviewsPage(long courseId, string sortBy, string sortDir, int? minRating, int page, int pageSize)
        => $"{Prefix}list:course:{courseId}:{sortBy.ToLowerInvariant()}:{sortDir.ToLowerInvariant()}:{minRating?.ToString() ?? "any"}:{page}:{pageSize}";

    public static string CourseTag(long courseId) => $"{Prefix}course:{courseId}";
}
