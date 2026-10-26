using Ardalis.Specification;

namespace Courses.Dal.Interfaces;

/// <summary>
/// Асинхронний узагальнений репозиторій — той самий контракт, що в Лаб. 02, але на EF Core.
/// Методи, що йдуть у базу, асинхронні; Update і Delete лише змінюють стан сутності в контексті.
/// Зберігає зміни не репозиторій, а Unit of Work — одним SaveChangesAsync на всю операцію.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    /// <param name="id">Значення ключа: long для курсів і викладачів, int для категорій.</param>
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Вибірка за специфікацією: фільтри, Include, сортування й сторінка описані поза репозиторієм.</summary>
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default);

    /// <summary>Кількість записів за критеріями специфікації — без сортування та сторінки.</summary>
    Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Delete(T entity);
}
