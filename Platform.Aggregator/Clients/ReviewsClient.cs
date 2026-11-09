using Reviews.Grpc;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Клієнт сервісу Reviews (Лаб. 04) поверх згенерованого gRPC-клієнта. Рейтинг і останні відгуки
/// всіх курсів замовлення — один виклик GetCoursesFeedback (у Лаб. 05 — по два HTTP-запити на курс).
/// </summary>
public sealed class ReviewsClient(ReviewsService.ReviewsServiceClient grpc, ILogger<ReviewsClient> logger) : GrpcUpstream(logger)
{
    private const int LatestReviewsCount = 3;

    public override string ServiceName => "reviews-api";

    public async Task<CourseFeedbackResponse?> GetFeedbackAsync(long courseId, CancellationToken ct)
        => (await GetFeedbackAsync([courseId], ct)).GetValueOrDefault(courseId);

    public async Task<IReadOnlyDictionary<long, CourseFeedbackResponse>> GetFeedbackAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken ct)
    {
        var reply = await CallAsync("GetCoursesFeedback", () => grpc.GetCoursesFeedbackAsync(
            new GetCoursesFeedbackRequest { CourseIds = { courseIds }, LatestCount = LatestReviewsCount }, Options(ct)));

        return (reply?.Courses ?? []).ToDictionary(course => course.CourseId, ToResponse);
    }

    private static CourseFeedbackResponse ToResponse(CourseFeedbackReply course) => new(
        course.CourseId,
        course.Rating is { } rating
            ? new CourseRatingResponse(
                rating.CourseId,
                rating.ReviewsCount,
                rating.HasAverageRating ? rating.AverageRating : null,
                rating.Distribution.ToDictionary(pair => pair.Key, pair => pair.Value))
            : null,
        [.. course.LatestReviews.Select(review => new ReviewItemResponse(
            review.Id,
            review.CourseId,
            review.AuthorName,
            review.Rating,
            review.HasText ? review.Text : null,
            review.IsVerifiedPurchase,
            review.CreatedAt.ToDateTime()))]);
}

// Дані відгуків, потрібні агрегатору (з protobuf-повідомлень)

public sealed record CourseRatingResponse(
    long CourseId,
    int ReviewsCount,
    double? AverageRating,
    IReadOnlyDictionary<string, int> Distribution);

public sealed record ReviewItemResponse(
    string Id,
    long CourseId,
    string AuthorName,
    int Rating,
    string? Text,
    bool IsVerifiedPurchase,
    DateTime CreatedAt);

/// <summary>Рейтинг і останні відгуки курсу.</summary>
public sealed record CourseFeedbackResponse(
    long CourseId,
    CourseRatingResponse? Rating,
    IReadOnlyList<ReviewItemResponse> LatestReviews);
