using System.Diagnostics;
using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Caching;
using Reviews.Application.Reviews.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewCommandHandler(
    IReviewRepository reviews,
    IMapper mapper,
    IReviewsCacheInvalidator cache,
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

        // Новий відгук змінює рейтинг і перші сторінки відгуків курсу — видаляємо їх із кешу одразу
        await cache.InvalidateCourseAsync(review.CourseId, "ReviewCreated", ct);

        // Бізнес-контекст у span TracingBehavior: за цими атрибутами trace шукається в Dashboard
        Activity.Current?.SetTag("review.id", review.Id.ToString());
        Activity.Current?.SetTag("course.id", review.CourseId);
        Activity.Current?.SetTag("review.rating", command.Rating);

        logger.LogInformation("Review {ReviewId} created for course {CourseId} by student {StudentId} with rating {Rating}",
            review.Id, review.CourseId, review.Author.StudentId, command.Rating);
        return mapper.Map<ReviewDto>(review);
    }
}
