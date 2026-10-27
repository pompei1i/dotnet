using Reviews.Application.Common.Interfaces;
using Reviews.Application.Discussions.Dtos;

namespace Reviews.Application.Discussions.Commands.CreateDiscussion;

/// <summary>
/// Нова тема курсу: питання ("question") або оголошення викладача ("announcement").
/// SourceReviewId — відгук, з якого виросла розмова (необов'язково).
/// </summary>
public sealed record CreateDiscussionCommand(
    long CourseId,
    string CourseTitle,
    string Title,
    string Type,
    long AuthorId,
    string AuthorRole,
    string AuthorName,
    IReadOnlyList<string>? Tags,
    string? SourceReviewId) : ICommand<DiscussionDto>;
