using FluentValidation;
using Reviews.Domain.Entities;

namespace Reviews.Application.Catalog.Commands.SyncCourseTitle;

/// <summary>Некоректна подія від повтору не стане коректною — обробник WebApi відправить її в DLQ одразу.</summary>
public sealed class SyncCourseTitleCommandValidator : AbstractValidator<SyncCourseTitleCommand>
{
    public SyncCourseTitleCommandValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.CourseId).GreaterThan(0);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Review.MaxCourseTitleLength);
        RuleFor(command => command.OccurredAt).NotEmpty();
    }
}
