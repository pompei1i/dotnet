using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>
/// Огляд курсу для викладача: каталог, відгуки й продажі. Усі три виклики незалежні, тож ідуть
/// одночасно (Task.WhenAll); будь-який із сервісів може бути недоступним — відповідь буде частковою.
/// </summary>
public sealed class CourseOverviewService(
    EnrollmentsClient enrollments,
    CoursesClient courses,
    ReviewsClient reviews,
    AggregateCache cache,
    ILogger<CourseOverviewService> logger)
{
    /// <summary>
    /// null — курсу немає в каталозі (каталог доступний і відповів NOT_FOUND). Повний огляд кешується
    /// в Redis на 30 с; частковий — ні.
    /// </summary>
    public Task<CourseOverviewDto?> GetOverviewAsync(long courseId, CancellationToken ct)
        => cache.GetOrBuildAsync(AggregateCache.CourseOverviewKey(courseId), () => BuildOverviewAsync(courseId, ct),
            overview => overview.Warnings.Count == 0, ct);

    private async Task<CourseOverviewDto?> BuildOverviewAsync(long courseId, CancellationToken ct)
    {
        using var activity = AggregatorTelemetry.Source.StartActivity("BuildCourseOverview");
        activity?.SetTag("course.id", courseId);

        var context = new AggregationContext(logger, ct);

        var courseTask = context.SafeAsync(courses.ServiceName, () => courses.GetCourseAsync(courseId, ct));
        var feedbackTask = context.SafeAsync(reviews.ServiceName, () => reviews.GetFeedbackAsync(courseId, ct));
        var salesTask = context.SafeAsync(enrollments.ServiceName, () => enrollments.GetCourseSalesAsync(courseId, ct));

        await Task.WhenAll(courseTask, feedbackTask, salesTask);

        // Каталог відповів, але курсу немає -> 404; каталог недоступний -> часткова відповідь
        var course = await courseTask;
        if (course is null && context.Responded(courses.ServiceName))
            return null;

        var feedback = await feedbackTask;
        var sales = await salesTask;

        // Продажі рахуються за локальним довідником курсів enrollments-api: курсу може там ще не бути
        if (sales is null && context.Responded(enrollments.ServiceName))
            context.Warn($"Course {courseId} has no sales data in enrollments-api yet");

        var overview = new CourseOverviewDto(
            courseId,
            course?.ToCourseInfo(),
            feedback?.ToCourseFeedback(),
            sales?.ToCourseSales(),
            context.Sources,
            context.Warnings);

        logger.LogInformation("Course overview {CourseId} built with {WarningsCount} warning(s)", courseId, overview.Warnings.Count);
        return overview;
    }
}
