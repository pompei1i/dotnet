using Ardalis.Specification;
using Courses.Domain.Entities;

namespace Courses.Bll.Specifications;

/// <summary>
/// Друга специфікація з іншими критеріями — курси одного викладача, новіші публікації першими.
/// Виконується тим самим методом репозиторію ListAsync, без жодних змін у репозиторії.
/// </summary>
public sealed class InstructorCoursesSpec : Specification<Course>
{
    public InstructorCoursesSpec(long instructorId, bool publishedOnly)
    {
        Query.AsNoTracking();
        Query.Where(course => course.InstructorId == instructorId);

        if (publishedOnly)
            Query.Where(course => course.IsPublished);

        Query.Include(course => course.Instructor);
        Query.Include(course => course.CourseCategories).ThenInclude(link => link.Category);

        Query.OrderByDescending(course => course.PublishedAt).ThenBy(course => course.Title);
    }
}
