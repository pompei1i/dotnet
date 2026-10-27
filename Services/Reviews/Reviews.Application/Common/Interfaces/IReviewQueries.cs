using MongoDB.Bson;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Поле сортування списку відгуків.</summary>
public enum ReviewSortField
{
    CreatedAt,
    Rating
}

/// <summary>Фільтр, сортування й сторінка списку відгуків — усе виконується в MongoDB.</summary>
public sealed record ReviewListCriteria(
    long? CourseId,
    long? StudentId,
    int? MinRating,
    string? Search,
    ReviewSortField SortBy,
    bool Descending,
    int Page,
    int PageSize);

/// <summary>
/// Читання відгуків для API (бік Query у CQRS). Обробники запитів залежать від цього інтерфейсу,
/// а не від IMongoCollection; реалізація в Infrastructure повертає з бази одразу DTO.
/// М'яко видалені відгуки не повертаються.
/// </summary>
public interface IReviewQueries
{
    Task<ReviewDto?> GetByIdAsync(ObjectId id, CancellationToken ct);

    Task<PagedResult<ReviewListItemDto>> GetListAsync(ReviewListCriteria criteria, CancellationToken ct);

    Task<CourseRatingDto> GetCourseRatingAsync(long courseId, CancellationToken ct);
}
