using Courses.Domain.Entities;

namespace Courses.Dal.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    /// <summary>
    /// EXPLICIT LOADING: спершу сама категорія (1-й запит), потім її курси окремим
    /// запитом (2-й) — лише коли вони справді потрібні.
    /// </summary>
    Task<Category?> GetWithCoursesAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Category>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<bool> ExistsByNameOrSlugAsync(string name, string slug, CancellationToken ct = default);
}
