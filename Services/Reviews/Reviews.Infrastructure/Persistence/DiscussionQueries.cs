using AutoMapper;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Discussions.Dtos;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Читання тем обговорення: список — aggregation pipeline з проєкцією в DTO.</summary>
public sealed class DiscussionQueries(MongoDbContext context, IMapper mapper) : IDiscussionQueries
{
    private static readonly FilterDefinitionBuilder<Discussion> Filter = Builders<Discussion>.Filter;
    private static readonly SortDefinitionBuilder<Discussion> Sort = Builders<Discussion>.Sort;

    private readonly IMongoCollection<Discussion> _discussions = context.Discussions;

    internal static readonly BsonDocument ListProjection = new()
    {
        { "_id", new BsonDocument("$toString", "$_id") },
        { "courseId", 1 },
        { "courseTitle", 1 },
        { "title", 1 },
        { "type", 1 },
        { "authorName", "$author.displayName" },
        { "status", 1 },
        { "tags", PagedAggregation.IfNull("$tags", new BsonArray()) },
        { "messagesCount", PagedAggregation.IfNull("$messagesCount", 0) },
        { "lastMessageAt", PagedAggregation.IfNull("$lastMessage.createdAt", BsonNull.Value) },
        { "isPinned", PagedAggregation.IfNull("$isPinned", BsonNull.Value) },
        { "createdAt", 1 },
        { "version", PagedAggregation.IfNull("$version", 1) }
    };

    public async Task<DiscussionDto?> GetByIdAsync(ObjectId id, CancellationToken ct)
    {
        var discussion = await _discussions.Find(Filter.Eq(d => d.Id, id)).FirstOrDefaultAsync(ct);
        return discussion is null ? null : mapper.Map<DiscussionDto>(discussion);
    }

    public Task<PagedResult<DiscussionListItemDto>> GetListAsync(DiscussionListCriteria criteria, CancellationToken ct)
        => PagedAggregation.ExecuteAsync<Discussion, DiscussionListItemDto>(
            _discussions, BuildListPipeline(criteria), criteria.Page, criteria.PageSize, ct);

    internal static BsonDocument[] BuildListPipeline(DiscussionListCriteria criteria)
    {
        var filter = Filter.Empty;
        if (criteria.CourseId is { } courseId)
            filter &= Filter.Eq(d => d.CourseId, courseId);
        if (criteria.Status is { } status)
            filter &= Filter.Eq(d => d.Status, status);
        if (criteria.Tag is { } tag)
            filter &= Filter.Eq("tags", tag);   // рівність із масивом: тема має такий тег (multikey-індекс)
        if (criteria.Search is { } search)
            filter &= Filter.Text(search, new TextSearchOptions { Language = "none" });   // ix_discussions_title_text

        // Спершу теми з останньою активністю — порядок індексу { courseId, status, updatedAt: -1 }
        var sort = Sort.Combine(Sort.Descending(d => d.UpdatedAt), Sort.Descending(d => d.Id));

        return PagedAggregation.BuildPipeline(filter, sort, ListProjection, criteria.Page, criteria.PageSize);
    }
}
