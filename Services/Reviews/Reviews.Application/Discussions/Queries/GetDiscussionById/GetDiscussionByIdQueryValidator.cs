using FluentValidation;
using Reviews.Application.Common.Validation;

namespace Reviews.Application.Discussions.Queries.GetDiscussionById;

public sealed class GetDiscussionByIdQueryValidator : AbstractValidator<GetDiscussionByIdQuery>
{
    public GetDiscussionByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
