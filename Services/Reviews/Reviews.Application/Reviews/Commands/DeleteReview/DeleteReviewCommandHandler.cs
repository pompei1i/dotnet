using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common;
using Reviews.Application.Common.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public sealed class DeleteReviewCommandHandler(
    IReviewRepository reviews,
    ILogger<DeleteReviewCommandHandler> logger) : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(command.Id), ct);
        if (review is null || review.IsDeleted)
            throw new NotFoundException("Review", command.Id);

        Concurrency.EnsureVersion(review.Version, command.ExpectedVersion, "Review", command.Id);

        review.Delete();
        await reviews.UpdateAsync(review, ct);

        logger.LogInformation("Review {ReviewId} deleted", review.Id);
    }
}
