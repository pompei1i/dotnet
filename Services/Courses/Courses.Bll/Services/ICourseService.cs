using Courses.Bll.Dtos;

namespace Courses.Bll.Services;

public interface ICourseService
{
    /// <summary>Сторінка каталогу з фільтрами й сортуванням — усе виконується в базі.</summary>
    Task<PagedResult<CourseDto>> GetListAsync(CourseQuery query, CancellationToken ct = default);

    Task<CourseDetailsDto> GetByIdAsync(long id, CancellationToken ct = default);

    Task<CourseDetailsDto> CreateAsync(CreateCourseDto input, CancellationToken ct = default);

    Task ChangePriceAsync(long id, ChangePriceDto input, CancellationToken ct = default);

    Task PublishAsync(long id, CancellationToken ct = default);

    Task UnpublishAsync(long id, CancellationToken ct = default);

    Task<LessonDto> AddLessonAsync(long courseId, CreateLessonDto input, CancellationToken ct = default);

    Task AssignCategoryAsync(long courseId, AssignCategoryDto input, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
