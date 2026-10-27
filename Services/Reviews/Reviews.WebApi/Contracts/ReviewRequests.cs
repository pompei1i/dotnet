namespace Reviews.WebApi.Contracts;

/// <summary>Тіло POST /api/reviews.</summary>
public sealed record CreateReviewRequest(
    long CourseId,
    string CourseTitle,
    long StudentId,
    string AuthorName,
    int Rating,
    string? Text,
    IReadOnlyList<string>? Pros,
    IReadOnlyList<string>? Cons);

/// <summary>Тіло PUT /api/reviews/{id}; очікувана версія — у заголовку If-Match.</summary>
public sealed record UpdateReviewRequest(
    int Rating,
    string? Text,
    IReadOnlyList<string>? Pros,
    IReadOnlyList<string>? Cons);

/// <summary>Тіло POST /api/reviews/{id}/comments. Role: "student" або "instructor".</summary>
public sealed record AddCommentRequest(long UserId, string Role, string DisplayName, string Text);

/// <summary>Параметри рядка запиту GET /api/reviews.</summary>
public sealed class ReviewListRequest
{
    public long? CourseId { get; init; }

    public long? StudentId { get; init; }

    /// <summary>Мінімальна оцінка, 1–5.</summary>
    public int? MinRating { get; init; }

    /// <summary>Повнотекстовий пошук по тексту відгуку (text-індекс).</summary>
    public string? Search { get; init; }

    /// <summary>createdAt або rating.</summary>
    public string SortBy { get; init; } = "createdAt";

    /// <summary>asc або desc.</summary>
    public string SortDir { get; init; } = "desc";

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
