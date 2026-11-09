namespace Platform.Aggregator.Clients;

/// <summary>Typed-клієнт каталогу Courses (Лаб. 03): актуальні дані курсів.</summary>
public sealed class CoursesClient(HttpClient http, ILogger<CoursesClient> logger) : UpstreamClient(http, logger)
{
    public override string ServiceName => "courses-api";

    public Task<CourseResponse?> GetCourseAsync(long courseId, CancellationToken ct)
        => GetAsync<CourseResponse>($"/api/courses/{courseId}", ct);

    /// <summary>
    /// Курси за списком Id — паралельно, бо пакетного ендпоінта в каталозі немає.
    /// Значення null — курсу з таким Id у каталозі немає (404).
    /// </summary>
    public async Task<IReadOnlyDictionary<long, CourseResponse?>> GetCoursesAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken ct)
    {
        var calls = courseIds.Select(async id => (Id: id, Course: await GetCourseAsync(id, ct)));
        var results = await Task.WhenAll(calls);
        return results.ToDictionary(result => result.Id, result => result.Course);
    }
}

// Відповідь courses-api (CourseDetailsDto) — лише поля, потрібні агрегатору

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
    IReadOnlyList<LessonResponse>? Lessons);

public sealed record InstructorResponse(long Id, string FullName);

public sealed record CourseDetailResponse(decimal DurationHours, bool HasCertificate);

public sealed record LessonResponse(long Id);
