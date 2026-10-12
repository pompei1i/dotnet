namespace Enrollments.Dal.Interfaces;

/// <summary>
/// Узагальнений контракт CRUD для сутностей із числовим ключем Id.
/// Методи повертають моделі, а не SqlDataReader чи DataTable: шари вище не знають про ADO.NET.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <returns>Id створеного запису.</returns>
    Task<long> AddAsync(T entity, CancellationToken ct = default);

    /// <returns>false, якщо запису з таким Id немає.</returns>
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
