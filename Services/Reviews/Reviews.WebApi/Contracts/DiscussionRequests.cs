namespace Reviews.WebApi.Contracts;

/// <summary>Тіло POST /api/discussions. Type: "question" | "announcement"; AuthorRole: "student" | "instructor".</summary>
public sealed record CreateDiscussionRequest(
    long CourseId,
    string CourseTitle,
    string Title,
    string Type,
    long AuthorId,
    string AuthorRole,
    string AuthorName,
    IReadOnlyList<string>? Tags,
    string? SourceReviewId);

/// <summary>Тіло PUT /api/discussions/{id}/status. Status: "open" | "resolved" | "closed".</summary>
public sealed record ChangeDiscussionStatusRequest(string Status);

/// <summary>Параметри рядка запиту GET /api/discussions.</summary>
public sealed class DiscussionListRequest
{
    public long? CourseId { get; init; }

    /// <summary>open, resolved або closed.</summary>
    public string? Status { get; init; }

    public string? Tag { get; init; }

    /// <summary>Повнотекстовий пошук по заголовку теми.</summary>
    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
