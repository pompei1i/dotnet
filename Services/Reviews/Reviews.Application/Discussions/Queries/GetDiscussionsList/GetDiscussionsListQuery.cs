using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Discussions.Dtos;

namespace Reviews.Application.Discussions.Queries.GetDiscussionsList;

/// <summary>Теми курсу: фільтр за станом і тегом, пошук по заголовку; спершу з останньою активністю.</summary>
public sealed record GetDiscussionsListQuery(
    long? CourseId = null,
    string? Status = null,
    string? Tag = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<DiscussionListItemDto>>;
