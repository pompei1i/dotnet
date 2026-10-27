using FluentValidation;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Validation;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

public sealed class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0).When(x => x.CourseId.HasValue);
        RuleFor(x => x.StudentId).GreaterThan(0).When(x => x.StudentId.HasValue);
        RuleFor(x => x.MinRating).InclusiveBetween(Rating.Min, Rating.Max).When(x => x.MinRating.HasValue);
        RuleFor(x => x.Search).MaximumLength(ValidationRules.MaxSearchLength);
        RuleFor(x => x.SortBy)
            .Must(sortBy => Enum.TryParse<ReviewSortField>(sortBy, ignoreCase: true, out var field)
                            && Enum.IsDefined(field) && !char.IsDigit(sortBy[0]))
            .WithMessage("SortBy must be createdAt or rating");
        RuleFor(x => x.SortDir)
            .Must(dir => string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDir must be asc or desc");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, ValidationRules.MaxPageSize);
    }
}
