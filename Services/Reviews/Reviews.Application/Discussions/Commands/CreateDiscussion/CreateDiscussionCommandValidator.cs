using FluentValidation;
using MongoDB.Bson;
using Reviews.Application.Common.Validation;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Discussions.Commands.CreateDiscussion;

public sealed class CreateDiscussionCommandValidator : AbstractValidator<CreateDiscussionCommand>
{
    public CreateDiscussionCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.CourseTitle).ValidCourseTitle();
        RuleFor(x => x.Title).NotEmpty().Length(Discussion.MinTitleLength, Discussion.MaxTitleLength);
        RuleFor(x => x.Type).MustBeEnum<CreateDiscussionCommand, DiscussionType>();
        RuleFor(x => x.AuthorId).GreaterThan(0);
        RuleFor(x => x.AuthorRole).MustBeEnum<CreateDiscussionCommand, ParticipantRole>();
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(Author.MaxNameLength);
        RuleFor(x => x.Tags).MustBeShortList(Discussion.MaxTags, Discussion.MaxTagLength);

        // Посилання на інший документ: спершу формат ObjectId, потім — запит до MongoDB,
        // що такий відгук існує і належить тому самому курсу
        RuleFor(x => x.SourceReviewId)
            .Cascade(CascadeMode.Stop)
            .MustBeObjectId()
            .MustAsync(async (command, id, ct) =>
            {
                var review = await reviews.GetByIdAsync(ObjectId.Parse(id), ct);
                return review is { IsDeleted: false } && review.CourseId == command.CourseId;
            })
            .WithMessage("SourceReviewId must reference an existing review of the same course")
            .When(x => x.SourceReviewId is not null);
    }
}
