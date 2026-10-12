using Enrollments.Domain.Models;

namespace Enrollments.Dal.Interfaces;

/// <summary>
/// Репозиторій локальної копії курсів. CRUD успадковано від IGenericRepository,
/// тут — лише пошук із пагінацією та агрегація продажів.
/// </summary>
public interface ICourseRepository : IGenericRepository<Course>
{
    /// <summary>Курси, доступні для купівлі, з пошуком за назвою.</summary>
    Task<PagedResult<Course>> GetAvailableAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Скільки оплачених замовлень містять курс і на яку суму; null, якщо курсу немає.</summary>
    Task<CourseSales?> GetSalesAsync(long courseId, CancellationToken ct = default);
}
