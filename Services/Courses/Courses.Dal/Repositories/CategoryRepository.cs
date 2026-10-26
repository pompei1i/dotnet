using Courses.Dal.Interfaces;
using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courses.Dal.Repositories;

public class CategoryRepository(CoursesDbContext context) : GenericRepository<Category>(context), ICategoryRepository
{
    /// <summary>
    /// EXPLICIT LOADING: категорія і її курси читаються ДВОМА окремими запитами.
    /// Другий виконується лише тоді, коли курси справді потрібні, — на відміну від Include,
    /// який тягне все одразу.
    /// </summary>
    public async Task<Category?> GetWithCoursesAsync(int id, CancellationToken ct = default)
    {
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);          // 1-й запит
        if (category is null)
            return null;

        await Context.Entry(category)
            .Collection(c => c.CourseCategories)
            .Query()
            .Include(link => link.Course).ThenInclude(course => course.Instructor)
            .LoadAsync(ct);                                                          // 2-й запит

        return category;
    }

    public async Task<IReadOnlyList<Category>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
        => await Set.Where(category => ids.Contains(category.Id)).ToListAsync(ct);

    public Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(category => category.Slug == slug, ct);

    public Task<bool> ExistsByNameOrSlugAsync(string name, string slug, CancellationToken ct = default)
        => Set.AnyAsync(category => category.Name == name || category.Slug == slug, ct);
}
