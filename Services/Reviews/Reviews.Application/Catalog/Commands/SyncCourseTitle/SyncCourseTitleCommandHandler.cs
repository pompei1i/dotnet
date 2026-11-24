using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Caching;
using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Catalog.Commands.SyncCourseTitle;

/// <summary>
/// Ідемпотентне оновлення courseTitle (Лаб. 08). MongoDB у стенді — один сервер без replica set, тож
/// транзакції на кілька документів недоступні; ідемпотентність тримається на тому, що кожен крок
/// безпечно повторюється:
/// 1. EventId уже в processed_events — дублікат, нічого не робимо;
/// 2. catalog_courses: назва фіксується, лише якщо подія не старша за застосовану — інакше stale;
/// 3. UpdateMany courseTitle у відгуках і обговореннях — повтор дає той самий стан;
/// 4. інвалідація кешу курсу (назва є в сторінках відгуків) і позначка EventId як обробленого.
/// Якщо процес впаде між кроками, повторна доставка пройде кроки 2–4 ще раз з тим самим результатом.
/// </summary>
public sealed class SyncCourseTitleCommandHandler(
    ICatalogSyncStore store,
    IReviewsCacheInvalidator cache,
    ILogger<SyncCourseTitleCommandHandler> logger) : IRequestHandler<SyncCourseTitleCommand, CourseTitleSyncResult>
{
    public async Task<CourseTitleSyncResult> Handle(SyncCourseTitleCommand command, CancellationToken ct)
    {
        if (await store.IsProcessedAsync(command.EventId, ct))
        {
            logger.LogInformation("Duplicate {EventType} {EventId} for course {CourseId} skipped: already processed",
                command.EventType, command.EventId, command.CourseId);
            return CourseTitleSyncResult.Duplicate;
        }

        if (!await store.TryAdvanceCourseTitleAsync(command.CourseId, command.Title, command.OccurredAt, ct))
        {
            await store.MarkProcessedAsync(command.EventId, command.EventType, command.Consumer, command.OccurredAt, ct);
            logger.LogWarning("{EventType} {EventId} for course {CourseId} is older than the applied title; reviews unchanged",
                command.EventType, command.EventId, command.CourseId);
            return CourseTitleSyncResult.Stale;
        }

        var (reviews, discussions) = await store.UpdateCourseTitleAsync(command.CourseId, command.Title, ct);
        await cache.InvalidateCourseAsync(command.CourseId, "CourseRenamed", ct);
        await store.MarkProcessedAsync(command.EventId, command.EventType, command.Consumer, command.OccurredAt, ct);

        logger.LogInformation("Course {CourseId} renamed to '{Title}': courseTitle updated in {ReviewsUpdated} review(s) and {DiscussionsUpdated} discussion(s) from {EventType} {EventId}",
            command.CourseId, command.Title, reviews, discussions, command.EventType, command.EventId);
        return new CourseTitleSyncResult("applied", reviews, discussions);
    }
}
