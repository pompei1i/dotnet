using FluentValidation;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.AddComment;

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Role).MustBeEnum<AddCommentCommand, ParticipantRole>();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(Author.MaxNameLength);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Review.MaxCommentLength);
    }
}
