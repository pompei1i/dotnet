using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Enums;

namespace Reviews.Application.Discussions.Commands.ChangeDiscussionStatus;

public sealed class ChangeDiscussionStatusCommandValidator : AbstractValidator<ChangeDiscussionStatusCommand>
{
    public ChangeDiscussionStatusCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Status).MustBeEnum<ChangeDiscussionStatusCommand, DiscussionStatus>();
        RuleFor(x => x.ExpectedVersion).MustBeVersion();
    }
}
