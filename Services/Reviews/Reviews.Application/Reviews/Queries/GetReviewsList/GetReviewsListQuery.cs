using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

/// <summary>
/// Список відгуків із фільтрами (курс, студент, мінімальна оцінка, повнотекстовий пошук),
/// сортуванням (createdAt | rating, asc | desc) і пагінацією.
/// </summary>
public sealed record GetReviewsListQuery(
    long? CourseId = null,
    long? StudentId = null,
    int? MinRating = null,
    string? Search = null,
    string SortBy = "createdAt",
    string SortDir = "desc",
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReviewListItemDto>>;
