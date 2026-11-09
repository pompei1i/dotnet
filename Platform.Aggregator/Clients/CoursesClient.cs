using Courses.Grpc;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Клієнт каталогу Courses (Лаб. 03) поверх згенерованого gRPC-клієнта. Курси замовлення
/// запитуються одним викликом GetCoursesByIds — у Лаб. 05 це було N паралельних HTTP-запитів.
/// </summary>
public sealed class CoursesClient(CoursesService.CoursesServiceClient grpc, ILogger<CoursesClient> logger) : GrpcUpstream(logger)
{
    public override string ServiceName => "courses-api";

    public async Task<CourseResponse?> GetCourseAsync(long courseId, CancellationToken ct)
    {
        var reply = await CallAsync("GetCourse", () => grpc.GetCourseAsync(new GetCourseRequest { Id = courseId }, Options(ct)));
        return reply?.ToResponse();
    }

    /// <summary>Курси за списком Id. Значення null — курсу з таким Id у каталозі немає.</summary>
    public async Task<IReadOnlyDictionary<long, CourseResponse?>> GetCoursesAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken ct)
    {
        var reply = await CallAsync("GetCoursesByIds",
            () => grpc.GetCoursesByIdsAsync(new GetCoursesByIdsRequest { Ids = { courseIds } }, Options(ct)));

        var found = (reply?.Courses ?? []).ToDictionary(course => course.Id, course => course.ToResponse());
        return courseIds.ToDictionary(id => id, id => found.GetValueOrDefault(id));
    }
}

// Дані каталогу, потрібні агрегатору (з protobuf-повідомлення CourseReply)

public sealed record CourseResponse(
    long Id,
    string Title,
    string Slug,
    decimal Price,
    string Level,
    string Language,
    bool IsPublished,
    InstructorResponse Instructor,
    CourseDetailResponse? Detail,
    int LessonsCount);

public sealed record InstructorResponse(long Id, string FullName);

public sealed record CourseDetailResponse(decimal DurationHours, bool HasCertificate);

internal static class CoursesMapping
{
    public static CourseResponse ToResponse(this CourseReply course) => new(
        course.Id,
        course.Title,
        course.Slug,
        course.Price.ToDecimal(),
        course.Level,
        course.Language,
        course.IsPublished,
        new InstructorResponse(course.Instructor.Id, course.Instructor.FullName),
        // optional-поля proto3: Has* відрізняє «не задано» від 0 / false
        course.DurationHours is not null && course.HasHasCertificate
            ? new CourseDetailResponse(course.DurationHours.ToDecimal(), course.HasCertificate)
            : null,
        course.LessonsCount);
}
