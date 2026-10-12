using AutoMapper;
using Enrollments.Bll.Dtos;
using Enrollments.Dal;
using Enrollments.Domain.Exceptions;

namespace Enrollments.Bll.Services;

/// <summary>
/// Курси в цьому сервісі доступні лише для читання: це локальна копія довідника,
/// власником якого є сервіс Courses.
/// </summary>
public class CourseService(IUnitOfWork uow, IMapper mapper) : ICourseService
{
    public async Task<PagedResultDto<CourseDto>> GetAvailableAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var result = await uow.Courses.GetAvailableAsync(normalizedSearch, page, pageSize, ct);

        return new PagedResultDto<CourseDto>(
            mapper.Map<IReadOnlyList<CourseDto>>(result.Items), result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<CourseDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var course = await uow.Courses.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException($"Course {id} not found");

        return mapper.Map<CourseDto>(course);
    }

    public async Task<CourseSalesDto> GetSalesAsync(long id, CancellationToken ct = default)
    {
        var sales = await uow.Courses.GetSalesAsync(id, ct)
                    ?? throw new NotFoundException($"Course {id} not found");

        return mapper.Map<CourseSalesDto>(sales);
    }
}
