using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

public sealed class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewListItemDto>>
{
    public Task<PagedResult<ReviewListItemDto>> Handle(GetReviewsListQuery query, CancellationToken ct)
        => queries.GetListAsync(
            new ReviewListCriteria(
                query.CourseId,
                query.StudentId,
                query.MinRating,
                string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                Enum.Parse<ReviewSortField>(query.SortBy, ignoreCase: true),
                string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase),
                Math.Max(query.Page, 1),
                Math.Clamp(query.PageSize, 1, ValidationRules.MaxPageSize)),
            ct);
}
