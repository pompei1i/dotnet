using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews,
    IMapper mapper,
    ILogger<CreateReviewCommandHandler> logger) : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken ct)
    {
        var review = new Review(
            command.CourseId,
            command.CourseTitle,
            new Author(command.StudentId, command.AuthorName),
            new Rating(command.Rating),
            command.Text,
            command.Pros,
            command.Cons);

        // Одночасний другий відгук того самого студента відсіче унікальний індекс -> 409
        await reviews.AddAsync(review, ct);

        logger.LogInformation("Review {ReviewId} created for course {CourseId} by student {StudentId}",
            review.Id, review.CourseId, review.Author.StudentId);
        return mapper.Map<ReviewDto>(review);
    }
}
