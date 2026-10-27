using Reviews.Application.Common.Interfaces;
using Reviews.Application.Reviews.Dtos;

namespace Reviews.Application.Reviews.Commands.AddComment;

/// <summary>Коментар студента чи викладача до відгуку; Role — "student" або "instructor".</summary>
public sealed record AddCommentCommand(
    string ReviewId,
    long UserId,
    string Role,
    string DisplayName,
    string Text) : ICommand<ReviewDto>;
