using Enrollments.Domain.Models;

namespace Enrollments.Dal.Interfaces;

/// <summary>
/// Репозиторій студентів. Свідомо НЕ успадковує IGenericRepository: його реалізація написана
/// на чистому ADO.NET і має власний контракт — оновлення з перевіркою RowVersion та
/// м'яке видалення із зазначенням автора, яких в узагальненому CRUD немає.
/// </summary>
public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<Student?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Сторінка студентів із пошуком за початком імені або Email.</summary>
    Task<PagedResult<Student>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    /// <returns>Id створеного студента.</returns>
    Task<long> AddAsync(Student student, CancellationToken ct = default);

    /// <summary>Оновлює студента, якщо його RowVersion не змінився з моменту читання.</summary>
    /// <returns>false, якщо рядок не знайдено або його вже змінив хтось інший.</returns>
    Task<bool> UpdateAsync(Student student, CancellationToken ct = default);

    /// <summary>М'яке видалення (IsDeleted = 1).</summary>
    /// <returns>false, якщо студента з таким Id немає.</returns>
    Task<bool> DeleteAsync(long id, string? deletedBy, CancellationToken ct = default);
}
