using MediatR;
using Reviews.Application.Common;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Common.Validation;
using Reviews.Application.Discussions.Dtos;
using Reviews.Domain.Enums;

namespace Reviews.Application.Discussions.Queries.GetDiscussionsList;

public sealed class GetDiscussionsListQueryHandler(IDiscussionQueries queries)
    : IRequestHandler<GetDiscussionsListQuery, PagedResult<DiscussionListItemDto>>
{
    public Task<PagedResult<DiscussionListItemDto>> Handle(GetDiscussionsListQuery query, CancellationToken ct)
        => queries.GetListAsync(
            new DiscussionListCriteria(
                query.CourseId,
                query.Status is null ? null : EnumText.Parse<DiscussionStatus>(query.Status),
                string.IsNullOrWhiteSpace(query.Tag) ? null : query.Tag.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                Math.Max(query.Page, 1),
                Math.Clamp(query.PageSize, 1, ValidationRules.MaxPageSize)),
            ct);
}
