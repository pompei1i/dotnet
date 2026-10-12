using Enrollments.Bll.Dtos;

namespace Enrollments.Bll.Services;

public interface IStudentService
{
    Task<StudentDto> GetByIdAsync(long id, CancellationToken ct = default);

    Task<PagedResultDto<StudentDto>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    Task<StudentDto> CreateAsync(CreateStudentDto input, CancellationToken ct = default);

    Task<StudentDto> UpdateAsync(long id, UpdateStudentDto input, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
