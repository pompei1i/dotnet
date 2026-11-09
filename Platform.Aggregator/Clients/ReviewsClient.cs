namespace Platform.Aggregator.Clients;

/// <summary>Typed-клієнт сервісу Reviews (Лаб. 04): рейтинг і останні відгуки про курс.</summary>
public sealed class ReviewsClient(HttpClient http, ILogger<ReviewsClient> logger) : UpstreamClient(http, logger)
{
    private const int LatestReviewsCount = 3;

    public override string ServiceName => "reviews-api";

    public Task<CourseRatingResponse?> GetRatingAsync(long courseId, CancellationToken ct)
        => GetAsync<CourseRatingResponse>($"/api/courses/{courseId}/rating", ct);

    public Task<ReviewsPageResponse?> GetLatestReviewsAsync(long courseId, CancellationToken ct)
        => GetAsync<ReviewsPageResponse>(
            $"/api/reviews?courseId={courseId}&sortBy=createdAt&sortDir=desc&page=1&pageSize={LatestReviewsCount}", ct);

    /// <summary>Рейтинг і останні відгуки одного курсу — два незалежні запити, паралельно.</summary>
    public async Task<CourseFeedbackResponse> GetFeedbackAsync(long courseId, CancellationToken ct)
    {
        var ratingTask = GetRatingAsync(courseId, ct);
        var reviewsTask = GetLatestReviewsAsync(courseId, ct);
        await Task.WhenAll(ratingTask, reviewsTask);

        return new CourseFeedbackResponse(courseId, await ratingTask, (await reviewsTask)?.Items ?? []);
    }

    /// <summary>Відгуки для кількох курсів — усі курси паралельно.</summary>
    public async Task<IReadOnlyDictionary<long, CourseFeedbackResponse>> GetFeedbackAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken ct)
    {
        var results = await Task.WhenAll(courseIds.Select(id => GetFeedbackAsync(id, ct)));
        return results.ToDictionary(result => result.CourseId);
    }
}

// Відповіді reviews-api — лише поля, потрібні агрегатору

public sealed record CourseRatingResponse(
    long CourseId,
    int ReviewsCount,
    double? AverageRating,
    IReadOnlyDictionary<string, int> Distribution);

public sealed record ReviewsPageResponse(IReadOnlyList<ReviewItemResponse> Items, long TotalCount);

public sealed record ReviewItemResponse(
    string Id,
    long CourseId,
    string AuthorName,
    int Rating,
    string? Text,
    bool IsVerifiedPurchase,
    DateTime CreatedAt);

/// <summary>Рейтинг і останні відгуки курсу, зібрані з двох ендпоінтів reviews-api.</summary>
public sealed record CourseFeedbackResponse(
    long CourseId,
    CourseRatingResponse? Rating,
    IReadOnlyList<ReviewItemResponse> LatestReviews);
