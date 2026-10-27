using MongoDB.Bson;
using Reviews.Application.Common.Models;
using Reviews.Application.Discussions.Dtos;
using Reviews.Domain.Enums;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Фільтр списку тем: курс, стан, тег і повнотекстовий пошук по заголовку.</summary>
public sealed record DiscussionListCriteria(
    long? CourseId,
    DiscussionStatus? Status,
    string? Tag,
    string? Search,
    int Page,
    int PageSize);

/// <summary>Читання тем обговорення для API; реалізація — в Infrastructure.</summary>
public interface IDiscussionQueries
{
    Task<DiscussionDto?> GetByIdAsync(ObjectId id, CancellationToken ct);

    Task<PagedResult<DiscussionListItemDto>> GetListAsync(DiscussionListCriteria criteria, CancellationToken ct);
}
