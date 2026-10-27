using FluentValidation;

namespace Reviews.Application.Reviews.Queries.GetCourseRating;

public sealed class GetCourseRatingQueryValidator : AbstractValidator<GetCourseRatingQuery>
{
    public GetCourseRatingQueryValidator() => RuleFor(x => x.CourseId).GreaterThan(0);
}
