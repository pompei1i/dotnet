using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Enums;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.AddComment;

public sealed class AddCommentCommandHandler(
    IReviewRepository reviews,
    IMapper mapper,
    ILogger<AddCommentCommandHandler> logger) : IRequestHandler<AddCommentCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(AddCommentCommand command, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(command.ReviewId), ct);
        if (review is null || review.IsDeleted)
            throw new NotFoundException("Review", command.ReviewId);

        var author = new Participant(command.UserId, EnumText.Parse<ParticipantRole>(command.Role), command.DisplayName);
        review.AddComment(author, command.Text);
        await reviews.UpdateAsync(review, ct);

        logger.LogInformation("Comment added to review {ReviewId} by {Role} {UserId}; {CommentsCount} comment(s) now",
            review.Id, command.Role, command.UserId, review.Comments.Count);
        return mapper.Map<ReviewDto>(review);
    }
}
