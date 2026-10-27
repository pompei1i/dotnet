using Courses.Bll.Dtos;

namespace Courses.Bll.Services;

public interface IInstructorService
{
    Task<IReadOnlyList<InstructorDto>> GetAllAsync(CancellationToken ct = default);

    Task<InstructorDto> GetByIdAsync(long id, CancellationToken ct = default);

    Task<InstructorDto> CreateAsync(CreateInstructorDto input, CancellationToken ct = default);

    /// <summary>Курси викладача — друга специфікація через той самий метод репозиторію.</summary>
    Task<IReadOnlyList<CourseDto>> GetCoursesAsync(long id, bool publishedOnly, CancellationToken ct = default);
}
