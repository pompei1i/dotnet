using Enrollments.Bll.Dtos;

namespace Enrollments.Bll.Services;

public interface ICourseService
{
    Task<PagedResultDto<CourseDto>> GetAvailableAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    Task<CourseDto> GetByIdAsync(long id, CancellationToken ct = default);

    Task<CourseSalesDto> GetSalesAsync(long id, CancellationToken ct = default);
}
