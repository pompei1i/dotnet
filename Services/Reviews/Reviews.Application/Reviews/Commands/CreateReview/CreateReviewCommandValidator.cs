using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.CourseTitle).ValidCourseTitle();
        RuleFor(x => x.StudentId).GreaterThan(0);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(Author.MaxNameLength);
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Text).MaximumLength(Review.MaxTextLength);
        RuleFor(x => x.Pros).MustBeShortList(Review.MaxHighlights, Review.MaxHighlightLength);
        RuleFor(x => x.Cons).MustBeShortList(Review.MaxHighlights, Review.MaxHighlightLength);

        // Асинхронне правило із запитом до MongoDB: один відгук студента на курс.
        // Гонку двох одночасних запитів закриває унікальний індекс ux_reviews_course_student
        RuleFor(x => x.StudentId)
            .MustAsync(async (command, _, ct) => !await reviews.ExistsAsync(command.CourseId, command.StudentId, ct))
            .WithMessage(command => $"Student {command.StudentId} has already reviewed course {command.CourseId}")
            .When(x => x.CourseId > 0 && x.StudentId > 0);
    }
}
