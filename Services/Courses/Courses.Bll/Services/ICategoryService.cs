using Courses.Bll.Dtos;

namespace Courses.Bll.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Категорія з її курсами — Explicit Loading.</summary>
    Task<CategoryWithCoursesDto> GetWithCoursesAsync(int id, CancellationToken ct = default);

    /// <summary>Курси, додані в категорію після дати, — LINQ-запит через проміжну сутність.</summary>
    Task<IReadOnlyList<RecentCourseDto>> GetRecentlyAddedAsync(string slug, DateTime? since, CancellationToken ct = default);

    Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct = default);
}
