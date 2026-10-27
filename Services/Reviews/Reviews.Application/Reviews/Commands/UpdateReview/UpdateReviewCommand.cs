using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

/// <summary>
/// Автор змінює оцінку, текст, переваги й недоліки. ExpectedVersion — версія з заголовка If-Match;
/// без нього перевіряється лише одночасний запис.
/// </summary>
public sealed record UpdateReviewCommand(
    string Id,
    int Rating,
    string? Text,
    IReadOnlyList<string>? Pros,
    IReadOnlyList<string>? Cons,
    int? ExpectedVersion) : ICommand<ReviewDto>;
