using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Discussions.Dtos;

/// <summary>Тема повністю. Type: "question" | "announcement"; Status: "open" | "resolved" | "closed".</summary>
public sealed record DiscussionDto(
    string Id,
    long CourseId,
    string CourseTitle,
    string Title,
    string Type,
    ParticipantDto Author,
    string Status,
    IReadOnlyList<string> Tags,
    int MessagesCount,
    MessagePreviewDto? LastMessage,
    string? AcceptedAnswerId,
    string? SourceReviewId,
    bool? IsPinned,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version);

public sealed record MessagePreviewDto(string MessageId, string AuthorName, string TextPreview, DateTime CreatedAt);

/// <summary>Рядок списку тем — з бази вибираються лише ці поля.</summary>
public sealed record DiscussionListItemDto(
    string Id,
    long CourseId,
    string CourseTitle,
    string Title,
    string Type,
    string AuthorName,
    string Status,
    IReadOnlyList<string> Tags,
    int MessagesCount,
    DateTime? LastMessageAt,
    bool? IsPinned,
    DateTime CreatedAt,
    int Version);
