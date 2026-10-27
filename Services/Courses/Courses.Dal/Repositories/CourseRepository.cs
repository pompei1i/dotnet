using Courses.Dal.Interfaces;
using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courses.Dal.Repositories;

public class CourseRepository(CoursesDbContext context) : GenericRepository<Course>(context), ICourseRepository
{
    /// <summary>
    /// EAGER LOADING: Include / ThenInclude завантажують картку курсу з усім пов'язаним одразу.
    /// AsSingleQuery явно вимагає ОДИН SQL-запит із JOIN, навіть попри дві колекції.
    /// Курс відстежується контекстом, тож сервіс може його змінити й зберегти.
    /// </summary>
    public Task<Course?> GetWithDetailsAsync(long id, CancellationToken ct = default)
        => Set
            .Include(course => course.Instructor)
            .Include(course => course.Detail)
            .Include(course => course.Lessons.OrderBy(lesson => lesson.OrderIndex))
            .Include(course => course.CourseCategories).ThenInclude(link => link.Category)
            .AsSingleQuery()
            .FirstOrDefaultAsync(course => course.Id == id, ct);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
        => Set.IgnoreQueryFilters().AnyAsync(course => course.Slug == slug, ct);
}
