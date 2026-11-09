using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using MongoDB.Driver;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Reviews.Dtos;
using Reviews.Application.Reviews.Queries.GetCourseRating;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Exceptions;
using Reviews.Grpc;
using ValidationException = FluentValidation.ValidationException;

namespace Reviews.WebApi.Grpc;

/// <summary>
/// gRPC-адаптер над Application-шаром, як і контролери: перевіряє вхідні дані, надсилає запити
/// MediatR (з валідацією й кешем у pipeline), перетворює DTO на protobuf-повідомлення, а винятки —
/// на RpcException: NotFound, InvalidArgument, FailedPrecondition, Unavailable (MongoDB недоступна).
/// </summary>
public sealed class ReviewsGrpcService(ISender sender) : ReviewsService.ReviewsServiceBase
{
    public const int MaxCoursesPerRequest = 50;
    public const int DefaultLatestCount = 3;
    public const int MaxLatestCount = 20;

    public override async Task<CourseRatingReply> GetCourseRating(GetCourseRatingRequest request, ServerCallContext context)
    {
        if (request.CourseId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "course_id must be positive"));

        try
        {
            return ToReply(await sender.Send(new GetCourseRatingQuery(request.CourseId), context.CancellationToken));
        }
        catch (Exception exception) when (ToStatus(exception) is { } status)
        {
            throw new RpcException(status);
        }
    }

    public override async Task<CoursesFeedbackReply> GetCoursesFeedback(GetCoursesFeedbackRequest request, ServerCallContext context)
    {
        if (request.CourseIds.Count is 0 or > MaxCoursesPerRequest)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"course_ids must contain from 1 to {MaxCoursesPerRequest} values"));
        if (request.CourseIds.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Every course id must be positive"));
        if (request.LatestCount is < 0 or > MaxLatestCount)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"latest_count must be from 0 to {MaxLatestCount}"));

        var latestCount = request.LatestCount == 0 ? DefaultLatestCount : request.LatestCount;
        var ct = context.CancellationToken;

        try
        {
            // Курси незалежні — паралельно; для кожного рейтинг і останні відгуки теж паралельно
            var feedback = await Task.WhenAll(request.CourseIds.Distinct().Select(async courseId =>
            {
                var ratingTask = sender.Send(new GetCourseRatingQuery(courseId), ct);
                var latestTask = sender.Send(new GetReviewsListQuery(
                    CourseId: courseId, SortBy: "createdAt", SortDir: "desc", Page: 1, PageSize: latestCount), ct);
                await Task.WhenAll(ratingTask, latestTask);

                return new CourseFeedbackReply
                {
                    CourseId = courseId,
                    Rating = ToReply(await ratingTask),
                    LatestReviews = { (await latestTask).Items.Select(ToReply) }
                };
            }));

            return new CoursesFeedbackReply { Courses = { feedback } };
        }
        catch (Exception exception) when (ToStatus(exception) is { } status)
        {
            throw new RpcException(status);
        }
    }

    /// <summary>Винятки Application/Domain/MongoDB -> коди gRPC (аналог GlobalExceptionHandler для REST).</summary>
    private static Status? ToStatus(Exception exception) => exception switch
    {
        ValidationException validation => new Status(StatusCode.InvalidArgument,
            string.Join("; ", validation.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}"))),
        NotFoundException => new Status(StatusCode.NotFound, exception.Message),
        ConflictException or DomainException => new Status(StatusCode.FailedPrecondition, exception.Message),
        MongoConnectionException or TimeoutException => new Status(StatusCode.Unavailable, "Database unavailable"),
        _ => null
    };

    private static CourseRatingReply ToReply(CourseRatingDto rating)
    {
        var reply = new CourseRatingReply
        {
            CourseId = rating.CourseId,
            ReviewsCount = rating.ReviewsCount
        };
        foreach (var (stars, count) in rating.Distribution)
            reply.Distribution[stars] = count;
        if (rating.AverageRating is { } average)
            reply.AverageRating = average;
        return reply;
    }

    private static ReviewItemReply ToReply(ReviewListItemDto review)
    {
        var reply = new ReviewItemReply
        {
            Id = review.Id,
            CourseId = review.CourseId,
            AuthorName = review.AuthorName,
            Rating = review.Rating,
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(review.CreatedAt, DateTimeKind.Utc))
        };
        if (review.Text is { } text)
            reply.Text = text;
        return reply;
    }
}
