using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>Відповіді сервісів -> частини композитних DTO.</summary>
internal static class DtoMapping
{
    public static CourseInfoDto ToCourseInfo(this CourseResponse course) => new(
        course.Title,
        course.Slug,
        course.Level,
        course.Language,
        course.IsPublished,
        course.Price,
        course.Instructor.FullName,
        course.Detail?.DurationHours,
        course.Detail?.HasCertificate,
        course.LessonsCount);

    public static CourseFeedbackDto ToCourseFeedback(this CourseFeedbackResponse feedback) => new(
        feedback.Rating?.ReviewsCount ?? 0,
        feedback.Rating?.AverageRating is { } average ? Math.Round(average, 2) : null,
        feedback.Rating?.Distribution,
        [.. feedback.LatestReviews.Select(review => new ReviewSnippetDto(
            review.Id, review.AuthorName, review.Rating, review.Text, review.IsVerifiedPurchase, review.CreatedAt))]);

    public static CourseSalesDto ToCourseSales(this CourseSalesResponse sales) => new(sales.OrdersCount, sales.Revenue);
}
