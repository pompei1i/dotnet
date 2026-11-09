using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Caching;

/// <summary>Інвалідація кешу відгуків після запису; викликається обробниками команд.</summary>
public interface IReviewsCacheInvalidator
{
    /// <summary>
    /// Видаляє все, що залежить від відгуків курсу (рейтинг, сторінки списку), з L1 і Redis.
    /// Обробник чекає на завершення — клієнт отримує відповідь уже після інвалідації.
    /// </summary>
    Task InvalidateCourseAsync(long courseId, string reason, CancellationToken ct);
}

public sealed class ReviewsCacheInvalidator(
    HybridCache cache,
    ReviewsCacheMetrics metrics,
    ILogger<ReviewsCacheInvalidator> logger) : IReviewsCacheInvalidator
{
    public async Task InvalidateCourseAsync(long courseId, string reason, CancellationToken ct)
    {
        var tag = ReviewsCacheKeys.CourseTag(courseId);

        using var activity = ReviewsCacheMetrics.ActivitySource.StartActivity("cache Invalidate");
        activity?.SetTag("cache.tag", tag);
        activity?.SetTag("cache.invalidation_reason", reason);

        try
        {
            // Рейтинг видаляємо ще й за ключем: так він зникає з Redis одразу, а не лише логічно за тегом
            await cache.RemoveAsync(ReviewsCacheKeys.Rating(courseId), ct);
            await cache.RemoveByTagAsync(tag, ct);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            // Запис у MongoDB уже відбувся — не перетворюємо його на 500 через Redis. Застарілий запис
            // проживе щонайбільше свій TTL (L1 30–60 с, Redis до 10 хв); збій видно в логах і метриці
            metrics.Failed("Invalidate");
            logger.LogWarning("Cache invalidation of {CacheTag} failed ({FailureType}: {FailureMessage}), reason {InvalidationReason}; entries expire by TTL",
                tag, exception.GetType().Name, exception.GetBaseException().Message, reason);
            return;
        }

        metrics.Invalidated(reason);
        logger.LogInformation("Cache invalidated {CacheTag} (rating and review pages of course {CourseId}), reason {InvalidationReason}",
            tag, courseId, reason);
    }
}
