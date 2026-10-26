using Courses.Dal.Interfaces;
using Courses.Dal.ReadModels;
using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courses.Dal.Repositories;

/// <summary>
/// LINQ to Entities поверх проміжної сутності M:N. Запити йдуть від CourseCategory
/// і використовують її власні поля — те, чого не дало б автоматичне M:N в EF Core.
/// </summary>
public class CourseCategoryRepository(CoursesDbContext context)
    : GenericRepository<CourseCategory>(context), ICourseCategoryRepository
{
    /// <summary>
    /// Курси, додані в категорію після дати. Where, OrderBy і Select виконуються в базі
    /// одним запитом з JOIN на Categories і Courses; у пам'ять приходить лише результат.
    /// </summary>
    public async Task<IReadOnlyList<CategoryCourseEntry>> GetRecentlyAddedAsync(
        string categorySlug, DateTime since, CancellationToken ct = default)
        => await Set
            .AsNoTracking()
            .Where(link => link.Category.Slug == categorySlug && link.AddedAt >= since)
            .OrderByDescending(link => link.AddedAt)
            .Select(link => new CategoryCourseEntry(
                link.Course.Id,
                link.Course.Title,
                link.Course.Slug,
                link.Course.Price,
                link.Course.Level,
                link.Course.IsPublished,
                link.AddedAt,
                link.IsPrimary))
            .ToListAsync(ct);

    /// <summary>
    /// GROUP BY по проміжній сутності: кількість курсів у категорії та скільки з них «основні».
    /// Сортування стоїть до Select: після проєкції в конструктор record-а EF Core уже не може
    /// зіставити поле з колонкою і не перекладе запит у SQL.
    /// </summary>
    public async Task<IReadOnlyList<CategoryCourseCount>> CountCoursesPerCategoryAsync(CancellationToken ct = default)
        => await Set
            .AsNoTracking()
            .GroupBy(link => new { link.CategoryId, link.Category.Name, link.Category.Slug })
            .OrderBy(group => group.Key.Name)
            .Select(group => new CategoryCourseCount(
                group.Key.CategoryId,
                group.Key.Name,
                group.Key.Slug,
                group.Count(),
                group.Count(link => link.IsPrimary)))
            .ToListAsync(ct);
}
