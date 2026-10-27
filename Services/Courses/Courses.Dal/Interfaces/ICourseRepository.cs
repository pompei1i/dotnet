using Courses.Domain.Entities;

namespace Courses.Dal.Interfaces;

public interface ICourseRepository : IGenericRepository<Course>
{
    /// <summary>
    /// EAGER LOADING: курс разом з викладачем, описом, уроками й категоріями — одним SQL-запитом.
    /// </summary>
    Task<Course?> GetWithDetailsAsync(long id, CancellationToken ct = default);

    /// <summary>Чи зайнятий Slug — серед усіх курсів, зокрема м'яко видалених (індекс унікальний для всіх).</summary>
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
}
