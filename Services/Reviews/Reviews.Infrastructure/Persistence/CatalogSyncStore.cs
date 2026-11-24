using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// MongoDB-частина синхронізації з Каталогом (Лаб. 08):
/// - processed_events: { _id: EventId, eventType, consumer, occurredAt, processedAt } — унікальний _id;
/// - catalog_courses:  { _id: courseId, title, appliedAt } — read model останньої застосованої назви
///   (час події, а не час обробки: за ним відсіюються події, що прийшли не по порядку);
/// - reviews / discussions: courseTitle оновлюється UpdateMany за courseId.
/// </summary>
public sealed class CatalogSyncStore(MongoDbContext context) : ICatalogSyncStore
{
    public const string ProcessedEventsCollection = "processed_events";
    public const string CatalogCoursesCollection = "catalog_courses";

    private IMongoCollection<BsonDocument> ProcessedEvents => context.Database.GetCollection<BsonDocument>(ProcessedEventsCollection);

    private IMongoCollection<BsonDocument> CatalogCourses => context.Database.GetCollection<BsonDocument>(CatalogCoursesCollection);

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct)
        => ProcessedEvents.Find(Builders<BsonDocument>.Filter.Eq("_id", eventId.ToString())).AnyAsync(ct);

    public async Task<bool> TryAdvanceCourseTitleAsync(long courseId, string title, DateTime occurredAt, CancellationToken ct)
    {
        // Upsert із умовою «застосована подія не новіша за цю». Якщо документ курсу вже є і він новіший,
        // фільтр його не знаходить, upsert пробує вставити документ з тим самим _id і отримує duplicate key —
        // це і є ознака застарілої події. Рівність дозволена: повтор тієї самої події після збою проходить.
        var filter = Builders<BsonDocument>.Filter.Eq("_id", courseId)
                     & Builders<BsonDocument>.Filter.Lte("appliedAt", occurredAt);
        var update = Builders<BsonDocument>.Update
            .Set("title", title)
            .Set("appliedAt", occurredAt);

        try
        {
            await CatalogCourses.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<(long Reviews, long Discussions)> UpdateCourseTitleAsync(long courseId, string title, CancellationToken ct)
    {
        var reviews = await context.Reviews.UpdateManyAsync(
            Builders<Review>.Filter.Eq(review => review.CourseId, courseId),
            Builders<Review>.Update.Set(review => review.CourseTitle, title),
            cancellationToken: ct);

        var discussions = await context.Discussions.UpdateManyAsync(
            Builders<Discussion>.Filter.Eq(discussion => discussion.CourseId, courseId),
            Builders<Discussion>.Update.Set(discussion => discussion.CourseTitle, title),
            cancellationToken: ct);

        return (reviews.ModifiedCount, discussions.ModifiedCount);
    }

    public async Task MarkProcessedAsync(Guid eventId, string eventType, string consumer, DateTime occurredAt, CancellationToken ct)
    {
        var document = new BsonDocument
        {
            ["_id"] = eventId.ToString(),
            ["eventType"] = eventType,
            ["consumer"] = consumer,
            ["occurredAt"] = occurredAt,
            ["processedAt"] = DateTime.UtcNow
        };

        try
        {
            await ProcessedEvents.InsertOneAsync(document, cancellationToken: ct);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Паралельна доставка тієї самої події вже записала позначку — результат той самий
        }
    }
}
