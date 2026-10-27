using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

public sealed class UpdateReviewCommandHandler(
    IReviewRepository reviews,
    IMapper mapper,
    ILogger<UpdateReviewCommandHandler> logger) : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(command.Id), ct);
        if (review is null || review.IsDeleted)
            throw new NotFoundException("Review", command.Id);

        Concurrency.EnsureVersion(review.Version, command.ExpectedVersion, "Review", command.Id);

        review.Edit(new Rating(command.Rating), command.Text, command.Pros, command.Cons);
        await reviews.UpdateAsync(review, ct);

        logger.LogInformation("Review {ReviewId} updated to version {Version}", review.Id, review.Version);
        return mapper.Map<ReviewDto>(review);
    }
}
