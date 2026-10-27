using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Tests;

/// <summary>
/// Запити рендеряться в команди MongoDB без підключення до бази: фільтрація, сортування,
/// пагінація й проєкція виконуються сервером ($match, $sort, $skip, $limit, $project).
/// </summary>
public class QueryPipelineTests
{
    public QueryPipelineTests() => TestData.EnsureMappings();

    private static ReviewListCriteria Criteria(string? search = null, ReviewSortField sortBy = ReviewSortField.CreatedAt)
        => new(CourseId: 1, StudentId: 7, MinRating: 4, Search: search, SortBy: sortBy, Descending: true, Page: 3, PageSize: 10);

    [Fact]
    public void Review_list_filters_sorts_and_pages_in_mongodb()
    {
        var pipeline = ReviewQueries.BuildListPipeline(Criteria());

        Assert.Equal(BsonDocument.Parse("""
            { "$match": { "isDeleted": { "$ne": true }, "courseId": { "$numberLong": "1" },
                          "author.studentId": { "$numberLong": "7" }, "rating": { "$gte": 4 } } }
            """), pipeline[0]);

        var items = pipeline[1]["$facet"]["items"].AsBsonArray;
        Assert.Equal(BsonDocument.Parse("""{ "$sort": { "createdAt": -1, "_id": -1 } }"""), items[0]);
        Assert.Equal(new BsonDocument("$skip", 20), items[1]);
        Assert.Equal(new BsonDocument("$limit", 10), items[2]);
        Assert.Equal(ReviewQueries.ListProjection, items[3]["$project"]);
        Assert.Equal(BsonDocument.Parse("""{ "$count": "count" }"""), pipeline[1]["$facet"]["total"][0]);
    }

    [Fact]
    public void Text_search_is_in_the_first_match_stage()
    {
        var match = ReviewQueries.BuildListPipeline(Criteria(search: "асинхронність"))[0]["$match"].AsBsonDocument;

        Assert.Equal(BsonDocument.Parse("""{ "$search": "асинхронність", "$language": "none" }"""), match["$text"]);
    }

    [Fact]
    public void Sorting_by_rating_uses_the_stored_number()
    {
        var items = ReviewQueries.BuildListPipeline(Criteria(sortBy: ReviewSortField.Rating))[1]["$facet"]["items"].AsBsonArray;

        Assert.Equal(BsonDocument.Parse("""{ "$sort": { "rating": -1, "_id": -1 } }"""), items[0]);
    }

    [Fact]
    public void List_projection_returns_only_the_dto_fields()
    {
        Assert.Equal(
            ["_id", "courseId", "courseTitle", "studentId", "authorName", "rating", "text", "isVerifiedPurchase", "commentsCount", "createdAt", "version"],
            ReviewQueries.ListProjection.Names);
        Assert.Equal(new BsonDocument("$toString", "$_id"), ReviewQueries.ListProjection["_id"]);
    }

    [Fact]
    public void Course_rating_is_a_group_aggregation()
    {
        var pipeline = ReviewQueries.BuildRatingPipeline(2);

        Assert.Equal(BsonDocument.Parse("""{ "courseId": { "$numberLong": "2" }, "isDeleted": { "$ne": true } }"""), pipeline[0]["$match"]);
        var group = pipeline[1]["$group"].AsBsonDocument;
        Assert.Equal(new BsonDocument("$avg", "$rating"), group["averageRating"]);
        Assert.Equal(["_id", "reviewsCount", "averageRating", "r1", "r2", "r3", "r4", "r5"], group.Names);
    }

    [Fact]
    public void Discussion_list_filters_by_status_tag_and_title_text()
    {
        var pipeline = DiscussionQueries.BuildListPipeline(new DiscussionListCriteria(3, DiscussionStatus.Open, "ef-core", "міграції", 1, 20));

        Assert.Equal(BsonDocument.Parse("""
            { "courseId": { "$numberLong": "3" }, "status": "open", "tags": "ef-core",
              "$text": { "$search": "міграції", "$language": "none" } }
            """), pipeline[0]["$match"]);
        Assert.Equal(BsonDocument.Parse("""{ "$sort": { "updatedAt": -1, "_id": -1 } }"""), pipeline[1]["$facet"]["items"][0]);
    }

    [Fact]
    public void Version_filter_accepts_lab01_documents_without_version()
    {
        var args = new RenderArgs<Review>(MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<Review>(),
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry);

        Assert.Equal(BsonDocument.Parse("""{ "$or": [{ "version": 1 }, { "version": { "$exists": false } }] }"""),
            OptimisticConcurrency.VersionFilter<Review>(1).Render(args));
        Assert.Equal(BsonDocument.Parse("""{ "version": 3 }"""), OptimisticConcurrency.VersionFilter<Review>(3).Render(args));
    }

    [Fact]
    public void Index_definitions_match_db_p3_collections_js()
    {
        var reviewArgs = new RenderArgs<Review>(MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<Review>(),
            MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry);
        var reviews = IndexCreationService.ReviewIndexes()
            .ToDictionary(index => index.Options.Name, index => index.Keys.Render(reviewArgs));

        Assert.Equal(BsonDocument.Parse("""{ "courseId": 1, "createdAt": -1 }"""), reviews["ix_reviews_course_createdAt"]);
        Assert.Equal(BsonDocument.Parse("""{ "courseId": 1, "author.studentId": 1 }"""), reviews["ux_reviews_course_student"]);
        Assert.Equal(BsonDocument.Parse("""{ "author.studentId": 1 }"""), reviews["ix_reviews_student"]);
        Assert.Equal(BsonDocument.Parse("""{ "discussionId": 1 }"""), reviews["ix_reviews_discussion"]);
        Assert.Equal(BsonDocument.Parse("""{ "text": "text" }"""), reviews["ix_reviews_text"]);
        Assert.True(IndexCreationService.ReviewIndexes().Single(i => i.Options.Name == "ux_reviews_course_student").Options.Unique);

        var discussionNames = IndexCreationService.DiscussionIndexes().Select(index => index.Options.Name);
        Assert.Equal(["ix_discussions_course_status_updatedAt", "ix_discussions_tags", "ix_discussions_title_text"], discussionNames);
    }
}
