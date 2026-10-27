using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>
/// Створює індекси під час старту. Назви й параметри збігаються з db/p3/collections.js (Лаб. 01),
/// тож скрипт і сервіс не конфліктують: повторне створення наявного індексу MongoDB пропускає.
/// Вимикається MongoDb:CreateIndexesOnStartup.
/// </summary>
public sealed class IndexCreationService(
    MongoDbContext context,
    IOptions<MongoDbOptions> options,
    ILogger<IndexCreationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.CreateIndexesOnStartup)
            return;

        var reviewIndexes = await context.Reviews.Indexes.CreateManyAsync(ReviewIndexes(), ct);
        var discussionIndexes = await context.Discussions.Indexes.CreateManyAsync(DiscussionIndexes(), ct);

        logger.LogInformation("MongoDB indexes ensured: {ReviewIndexes} on {Reviews}; {DiscussionIndexes} on {Discussions}",
            reviewIndexes, MongoDbContext.ReviewsCollection, discussionIndexes, MongoDbContext.DiscussionsCollection);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    internal static IEnumerable<CreateIndexModel<Review>> ReviewIndexes()
    {
        var keys = Builders<Review>.IndexKeys;
        return
        [
            // відгуки курсу, найновіші перші: find({ courseId }).sort({ createdAt: -1 })
            new(keys.Ascending(r => r.CourseId).Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_reviews_course_createdAt" }),

            // один відгук студента на курс; ним же користується перевірка ExistsAsync
            new(keys.Ascending(r => r.CourseId).Ascending(r => r.Author.StudentId),
                new CreateIndexOptions { Name = "ux_reviews_course_student", Unique = true }),

            // «мої відгуки» та оновлення денормалізованого author.displayName
            new(keys.Ascending(r => r.Author.StudentId),
                new CreateIndexOptions { Name = "ix_reviews_student" }),

            // відгук, з якого виросла тема; частковий — лише документи, де поле є
            new(keys.Ascending(r => r.DiscussionId),
                new CreateIndexOptions<Review>
                {
                    Name = "ix_reviews_discussion",
                    PartialFilterExpression = Builders<Review>.Filter.Exists(r => r.DiscussionId)
                }),

            // повнотекстовий пошук по тексту відгуку; українська не має стемера, тож мова "none"
            new(keys.Text(r => r.Text),
                new CreateIndexOptions { Name = "ix_reviews_text", DefaultLanguage = "none" })
        ];
    }

    internal static IEnumerable<CreateIndexModel<Discussion>> DiscussionIndexes()
    {
        var keys = Builders<Discussion>.IndexKeys;
        return
        [
            // теми курсу з обраним станом, спершу з останньою активністю
            new(keys.Ascending(d => d.CourseId).Ascending(d => d.Status).Descending(d => d.UpdatedAt),
                new CreateIndexOptions { Name = "ix_discussions_course_status_updatedAt" }),

            // теми за тегом (multikey-індекс по масиву)
            new(keys.Ascending("tags"),
                new CreateIndexOptions { Name = "ix_discussions_tags" }),

            // повнотекстовий пошук по заголовках
            new(keys.Text(d => d.Title),
                new CreateIndexOptions { Name = "ix_discussions_title_text", DefaultLanguage = "none" })
        ];
    }
}
