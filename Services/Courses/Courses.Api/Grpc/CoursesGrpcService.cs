using Courses.Bll.Dtos;
using Courses.Bll.Services;
using Courses.Domain.Exceptions;
using Courses.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Courses.Api.Grpc;

/// <summary>
/// gRPC-адаптер над BLL каталогу — те саме, що контролер для REST: перевіряє вхідні дані, викликає
/// ICourseService (з кешем Лаб. 06), перетворює DTO на protobuf-повідомлення, а доменні винятки —
/// на RpcException з точним кодом: NotFound, InvalidArgument, FailedPrecondition. Несподівані помилки
/// GrpcLoggingInterceptor логує зі stack trace і повертає клієнту як Internal без деталей.
/// </summary>
public sealed class CoursesGrpcService(ICourseService courses) : CoursesService.CoursesServiceBase
{
    public const int MaxIdsPerRequest = 100;

    public override async Task<CourseReply> GetCourse(GetCourseRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            var course = await courses.GetByIdAsync(request.Id, context.CancellationToken);
            return ToReply(course);
        }
        catch (Exception exception) when (ToStatus(exception) is { } status)
        {
            throw new RpcException(status);
        }
    }

    public override async Task<CoursesReply> GetCoursesByIds(GetCoursesByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Count is 0 or > MaxIdsPerRequest)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Ids must contain from 1 to {MaxIdsPerRequest} values"));
        if (request.Ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Every id must be positive"));

        try
        {
            var found = await courses.GetByIdsAsync(request.Ids, context.CancellationToken);
            return new CoursesReply { Courses = { found.Select(ToReply) } };
        }
        catch (Exception exception) when (ToStatus(exception) is { } status)
        {
            throw new RpcException(status);
        }
    }

    /// <summary>Доменні винятки -> коди gRPC (аналог DomainExceptionHandler для REST).</summary>
    private static Status? ToStatus(Exception exception) => exception switch
    {
        EntityNotFoundException notFound => new Status(StatusCode.NotFound, notFound.Message),
        ValidationException invalid => new Status(StatusCode.InvalidArgument, invalid.Message),
        ConflictException or DomainException => new Status(StatusCode.FailedPrecondition, exception.Message),
        _ => null
    };

    private static CourseReply ToReply(CourseDetailsDto course)
    {
        var reply = new CourseReply
        {
            Id = course.Id,
            Title = course.Title,
            Slug = course.Slug,
            Price = DecimalValue.FromDecimal(course.Price),
            Level = course.Level,
            Language = course.Language,
            IsPublished = course.IsPublished,
            Instructor = new InstructorReply { Id = course.Instructor.Id, FullName = course.Instructor.FullName },
            LessonsCount = course.Lessons.Count,
            ReviewsCount = course.ReviewsCount
        };

        if (course.PublishedAt is { } publishedAt)
            reply.PublishedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(publishedAt, DateTimeKind.Utc));
        if (course.Detail is { } detail)
        {
            reply.DurationHours = DecimalValue.FromDecimal(detail.DurationHours);
            reply.HasCertificate = detail.HasCertificate;
        }
        if (course.AverageRating is { } rating)
            reply.AverageRating = (double)rating;

        return reply;
    }
}
