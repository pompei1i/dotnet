using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Reviews.Application.Discussions.Dtos;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Tests;

/// <summary>
/// BsonClassMap без атрибутів у домені дає документ зі структурою Лаб. 01: camelCase, вкладені
/// author і comments, Rating — число, перерахування — рядки, без внутрішніх полів .NET і null.
/// </summary>
public class BsonMappingTests
{
    public BsonMappingTests() => TestData.EnsureMappings();

    [Fact]
    public void Review_document_has_the_lab01_structure()
    {
        var review = TestData.Review(text: null);
        review.AddComment(TestData.Instructor, "Дякую");

        var document = review.ToBsonDocument();

        Assert.Equal(
            ["_id", "createdAt", "updatedAt", "version", "courseId", "courseTitle", "author", "rating", "isVerifiedPurchase", "isDeleted", "comments", "pros"],
            document.Names);
        Assert.Equal(BsonType.ObjectId, document["_id"].BsonType);
        Assert.Equal(BsonType.Int32, document["rating"].BsonType);
        Assert.Equal(new BsonDocument { { "studentId", 7L }, { "displayName", "Олена Коваль" } }, document["author"]);
        Assert.Equal("instructor", document["comments"][0]["author"]["role"].AsString);
        Assert.Equal(BsonType.DateTime, document["comments"][0]["createdAt"].BsonType);
    }

    [Fact]
    public void Document_has_no_private_fields_discriminator_or_nulls()
    {
        var json = TestData.Review(text: null).ToBsonDocument().ToJson();

        Assert.DoesNotContain("_comments", json);
        Assert.DoesNotContain("_t", json);
        Assert.DoesNotContain("null", json);           // text, updatedAt, discussionId відсутні, а не null
        Assert.DoesNotContain("photos", json);         // порожній масив не записується
        Assert.DoesNotContain("isInstructor", json);   // обчислювана властивість Participant
    }

    [Fact]
    public void Review_round_trips_through_bson()
    {
        var review = TestData.Review();
        review.AddComment(TestData.Student, "Підтверджую");
        review.BumpVersion();

        var restored = BsonSerializer.Deserialize<Review>(review.ToBsonDocument());

        Assert.Equal(review.Id, restored.Id);
        Assert.Equal(review.Author, restored.Author);
        Assert.Equal(review.Rating, restored.Rating);
        Assert.Equal(2, restored.Version);
        Assert.Equal(review.Pros, restored.Pros);
        Assert.Equal(TestData.Student, Assert.Single(restored.Comments).Author);
    }

    [Fact]
    public void Lab01_document_with_unknown_fields_and_without_version_is_readable()
    {
        var document = BsonDocument.Parse("""
            {
              "_id": { "$oid": "670000000000000000000202" },
              "courseId": 3, "courseTitle": "EF Core", "title": "Помилка міграції", "type": "question",
              "author": { "userId": 2, "role": "student", "displayName": "Максим" },
              "status": "open", "messagesCount": 2, "futureField": "ignored",
              "createdAt": { "$date": "2026-10-04T12:05:00Z" }
            }
            """);

        var discussion = BsonSerializer.Deserialize<Discussion>(document);

        Assert.Equal(DiscussionStatus.Open, discussion.Status);
        Assert.Equal(1, discussion.Version);           // поля version немає -> версія 1
        Assert.Empty(discussion.Tags);
        Assert.Equal(DateTimeKind.Utc, discussion.CreatedAt.Kind);
    }

    [Fact]
    public void Discussion_document_uses_lowercase_enums_and_omits_question_only_fields()
    {
        var document = TestData.Question().ToBsonDocument();

        Assert.Equal("question", document["type"].AsString);
        Assert.Equal("open", document["status"].AsString);
        Assert.Equal(new BsonArray { "csharp", "plan" }, document["tags"]);
        Assert.False(document.Contains("isPinned"));
        Assert.False(document.Contains("lastMessage"));
    }

    [Fact]
    public void Seed_files_match_db_p3_seed_js()
    {
        var reviews = SeedData.Load<Review>("reviews.json");
        var discussions = SeedData.Load<Discussion>("discussions.json");

        Assert.Equal(4, reviews.Count);
        Assert.Equal(3, discussions.Count);
        var first = reviews.Single(r => r.Id == ObjectId.Parse("670000000000000000000101"));
        Assert.Equal(2, first.Comments.Count);
        Assert.Equal(3, first.Version);
        Assert.Equal(ObjectId.Parse("670000000000000000000201"), first.DiscussionId);
        Assert.Equal("https://cdn.example.com/reviews/103/review.mp4", reviews.Single(r => r.VideoUrl is not null).VideoUrl);
        Assert.True(discussions.Single(d => d.Type == DiscussionType.Announcement).IsPinned);
        Assert.NotNull(discussions.Single(d => d.Status == DiscussionStatus.Resolved).LastMessage);
    }

    [Fact]
    public void Projected_document_deserializes_straight_into_dto_records()
    {
        var item = BsonSerializer.Deserialize<ReviewListItemDto>(BsonDocument.Parse("""
            { "_id": "670000000000000000000104", "courseId": 3, "courseTitle": "EF Core", "studentId": 4,
              "authorName": "Дмитро", "rating": 4, "text": null, "isVerifiedPurchase": false,
              "commentsCount": 0, "createdAt": { "$date": "2026-10-06T09:10:00Z" }, "version": 1 }
            """));
        Assert.Equal(new ReviewListItemDto("670000000000000000000104", 3, "EF Core", 4, "Дмитро", 4, null, false, 0,
            new DateTime(2026, 10, 6, 9, 10, 0, DateTimeKind.Utc), 1), item);

        var discussion = BsonSerializer.Deserialize<DiscussionListItemDto>(BsonDocument.Parse("""
            { "_id": "670000000000000000000203", "courseId": 5, "courseTitle": "Aspire", "title": "Оголошення",
              "type": "announcement", "authorName": "Тарас", "status": "closed", "tags": [], "messagesCount": 0,
              "lastMessageAt": null, "isPinned": true, "createdAt": { "$date": "2026-10-01T08:00:00Z" }, "version": 1 }
            """));
        Assert.True(discussion.IsPinned);
        Assert.Empty(discussion.Tags);
    }
}
