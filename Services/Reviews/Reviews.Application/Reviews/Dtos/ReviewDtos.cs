namespace Reviews.Application.Reviews.Dtos;

/// <summary>Відгук повністю — для картки відгуку. Id і посилання — рядкові ObjectId.</summary>
public sealed record ReviewDto(
    string Id,
    long CourseId,
    string CourseTitle,
    AuthorDto Author,
    int Rating,
    string? Text,
    bool IsVerifiedPurchase,
    IReadOnlyList<string> Photos,
    string? VideoUrl,
    IReadOnlyList<string> Pros,
    IReadOnlyList<string> Cons,
    IReadOnlyList<CommentDto> Comments,
    string? DiscussionId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version);

public sealed record AuthorDto(long StudentId, string DisplayName);

/// <summary>Учасник розмови; Role — "student" або "instructor".</summary>
public sealed record ParticipantDto(long UserId, string Role, string DisplayName);

public sealed record CommentDto(ParticipantDto Author, string Text, DateTime CreatedAt);

/// <summary>Рядок списку відгуків — лише поля, потрібні списку; MongoDB повертає саме їх.</summary>
public sealed record ReviewListItemDto(
    string Id,
    long CourseId,
    string CourseTitle,
    long StudentId,
    string AuthorName,
    int Rating,
    string? Text,
    bool IsVerifiedPurchase,
    int CommentsCount,
    DateTime CreatedAt,
    int Version);

/// <summary>Рейтинг курсу, обчислений aggregation pipeline за активними відгуками.</summary>
public sealed record CourseRatingDto(
    long CourseId,
    int ReviewsCount,
    double? AverageRating,
    IReadOnlyDictionary<string, int> Distribution);
