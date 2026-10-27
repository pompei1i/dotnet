using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Rating).InclusiveBetween(Rating.Min, Rating.Max);
        RuleFor(x => x.Text).MaximumLength(Review.MaxTextLength);
        RuleFor(x => x.Pros).MustBeShortList(Review.MaxHighlights, Review.MaxHighlightLength);
        RuleFor(x => x.Cons).MustBeShortList(Review.MaxHighlights, Review.MaxHighlightLength);
        RuleFor(x => x.ExpectedVersion).MustBeVersion();
    }
}
