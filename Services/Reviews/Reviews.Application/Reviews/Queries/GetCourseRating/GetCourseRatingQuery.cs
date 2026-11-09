using Reviews.Application.Caching;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Queries.GetCourseRating;

/// <summary>
/// Середня оцінка курсу й розподіл оцінок — aggregation pipeline ($match + $group).
/// Кешується (Лаб. 06): рейтинг читається з кожною карткою замовлення, а змінюється лише разом із відгуком.
/// </summary>
public sealed record GetCourseRatingQuery(long CourseId) : IQuery<CourseRatingDto>, ICacheableQuery
{
    string? ICacheableQuery.CacheKey => ReviewsCacheKeys.Rating(CourseId);

    IReadOnlyCollection<string> ICacheableQuery.CacheTags => [ReviewsCacheKeys.CourseTag(CourseId)];

    ReviewsCachePolicy ICacheableQuery.CachePolicy => ReviewsCachePolicy.Rating;
}
