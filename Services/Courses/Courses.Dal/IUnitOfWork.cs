using Courses.Dal.Interfaces;

namespace Courses.Dal;

/// <summary>
/// Одиниця роботи: усі репозиторії працюють з одним DbContext, а зміни потрапляють у базу
/// одним SaveChangesAsync — тобто в одній транзакції. Реєструється в DI як Scoped.
/// </summary>
public interface IUnitOfWork
{
    ICourseRepository Courses { get; }
    ICategoryRepository Categories { get; }
    ICourseCategoryRepository CourseCategories { get; }
    IInstructorRepository Instructors { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
