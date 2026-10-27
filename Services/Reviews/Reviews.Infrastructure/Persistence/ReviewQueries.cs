using AutoMapper;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Бік читання відгуків (Query у CQRS). Список і рейтинг — aggregation pipeline з $match
/// і проєкцією одразу у форму DTO; картка відгуку — один документ за _id через AutoMapper.
/// </summary>
public sealed class ReviewQueries(MongoDbContext context, IMapper mapper) : IReviewQueries
{
    private static readonly FilterDefinitionBuilder<Review> Filter = Builders<Review>.Filter;
    private static readonly SortDefinitionBuilder<Review> Sort = Builders<Review>.Sort;

    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    /// <summary>
    /// Поля рядка списку: з бази приходять лише вони. _id перетворюється на рядок ($toString),
    /// кількість коментарів рахує сервер ($size), відсутні поля отримують null через $ifNull.
    /// </summary>
    internal static readonly BsonDocument ListProjection = new()
    {
        { "_id", new BsonDocument("$toString", "$_id") },
        { "courseId", 1 },
        { "courseTitle", 1 },
        { "studentId", "$author.studentId" },
        { "authorName", "$author.displayName" },
        { "rating", 1 },
        { "text", PagedAggregation.IfNull("$text", BsonNull.Value) },
        { "isVerifiedPurchase", PagedAggregation.IfNull("$isVerifiedPurchase", false) },
        { "commentsCount", new BsonDocument("$size", PagedAggregation.IfNull("$comments", new BsonArray())) },
        { "createdAt", 1 },
        { "version", PagedAggregation.IfNull("$version", 1) }
    };

    public async Task<ReviewDto?> GetByIdAsync(ObjectId id, CancellationToken ct)
    {
        var review = await _reviews.Find(Filter.Eq(r => r.Id, id) & NotDeleted).FirstOrDefaultAsync(ct);
        return review is null ? null : mapper.Map<ReviewDto>(review);
    }

    public Task<PagedResult<ReviewListItemDto>> GetListAsync(ReviewListCriteria criteria, CancellationToken ct)
        => PagedAggregation.ExecuteAsync<Review, ReviewListItemDto>(
            _reviews, BuildListPipeline(criteria), criteria.Page, criteria.PageSize, ct);

    /// <summary>
    /// Рейтинг курсу: $match за курсом -> $group з середнім і кількістю відгуків на кожну оцінку.
    /// </summary>
    public async Task<CourseRatingDto> GetCourseRatingAsync(long courseId, CancellationToken ct)
    {
        var result = await _reviews
            .Aggregate(PipelineDefinition<Review, BsonDocument>.Create(BuildRatingPipeline(courseId)), cancellationToken: ct)
            .FirstOrDefaultAsync(ct);

        var distribution = Enumerable.Range(Rating.Min, Rating.Max - Rating.Min + 1)
            .ToDictionary(score => score.ToString(), score => result?[$"r{score}"].ToInt32() ?? 0);

        return new CourseRatingDto(
            courseId,
            result?["reviewsCount"].ToInt32() ?? 0,
            result is null ? null : Math.Round(result["averageRating"].ToDouble(), 2),
            distribution);
    }

    internal static BsonDocument[] BuildListPipeline(ReviewListCriteria criteria)
    {
        var filter = NotDeleted;
        if (criteria.CourseId is { } courseId)
            filter &= Filter.Eq(r => r.CourseId, courseId);
        if (criteria.StudentId is { } studentId)
            filter &= Filter.Eq(r => r.Author.StudentId, studentId);
        if (criteria.MinRating is { } minRating)
            filter &= Filter.Gte(r => r.Rating, new Rating(minRating));
        if (criteria.Search is { } search)
            filter &= Filter.Text(search, new TextSearchOptions { Language = "none" });   // індекс ix_reviews_text

        SortDefinition<Review> primary = criteria.SortBy switch
        {
            ReviewSortField.Rating => criteria.Descending ? Sort.Descending(r => r.Rating) : Sort.Ascending(r => r.Rating),
            _ => criteria.Descending ? Sort.Descending(r => r.CreatedAt) : Sort.Ascending(r => r.CreatedAt)
        };
        // Однакові значення поля сортування не повинні «перескакувати» між сторінками: дотискаємо за _id
        var sort = Sort.Combine(primary, Sort.Descending(r => r.Id));

        return PagedAggregation.BuildPipeline(filter, sort, ListProjection, criteria.Page, criteria.PageSize);
    }

    internal static BsonDocument[] BuildRatingPipeline(long courseId)
    {
        var match = (Filter.Eq(r => r.CourseId, courseId) & NotDeleted).Render(PagedAggregation.RenderArgs<Review>());

        var group = new BsonDocument
        {
            { "_id", "$courseId" },
            { "reviewsCount", new BsonDocument("$sum", 1) },
            { "averageRating", new BsonDocument("$avg", "$rating") }
        };
        for (var score = Rating.Min; score <= Rating.Max; score++)
            group.Add($"r{score}", new BsonDocument("$sum",
                new BsonDocument("$cond", new BsonArray { new BsonDocument("$eq", new BsonArray { "$rating", score }), 1, 0 })));

        return [new BsonDocument("$match", match), new BsonDocument("$group", group)];
    }

    /// <summary>М'яко видалені відгуки не читаються; документи без поля isDeleted вважаються активними.</summary>
    private static FilterDefinition<Review> NotDeleted => Filter.Ne(r => r.IsDeleted, true);
}
