using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Commands.CreateReview;

/// <summary>
/// Студент залишає відгук про курс. CourseTitle і AuthorName — денормалізовані копії
/// з CoursesDb та EnrollmentsDb; IsVerifiedPurchase клієнт не задає.
/// </summary>
public sealed record CreateReviewCommand(
    long CourseId,
    string CourseTitle,
    long StudentId,
    string AuthorName,
    int Rating,
    string? Text,
    IReadOnlyList<string>? Pros,
    IReadOnlyList<string>? Cons) : ICommand<ReviewDto>;
