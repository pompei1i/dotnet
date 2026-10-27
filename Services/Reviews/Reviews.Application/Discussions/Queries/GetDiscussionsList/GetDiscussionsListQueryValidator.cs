using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;

namespace Reviews.Application.Discussions.Queries.GetDiscussionsList;

public sealed class GetDiscussionsListQueryValidator : AbstractValidator<GetDiscussionsListQuery>
{
    public GetDiscussionsListQueryValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0).When(x => x.CourseId.HasValue);
        RuleFor(x => x.Status).MustBeEnum<GetDiscussionsListQuery, DiscussionStatus>().When(x => x.Status is not null);
        RuleFor(x => x.Tag).MaximumLength(Discussion.MaxTagLength);
        RuleFor(x => x.Search).MaximumLength(ValidationRules.MaxSearchLength);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, ValidationRules.MaxPageSize);
    }
}
