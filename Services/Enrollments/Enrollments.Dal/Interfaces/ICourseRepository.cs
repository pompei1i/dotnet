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

    /// <summary>
    /// Застосовує знімок курсу з події Каталогу (Лаб. 08) до локальної копії: Title, Price, IsAvailable.
    /// Оновлює рядок, лише якщо подія новіша за SyncedAt — подія, що прийшла пізніше за новішу (out-of-order),
    /// не перезапише свіжі дані. Курсу, якого ще немає, — додає. Ціни в оформлених замовленнях
    /// (OrderItems.UnitPrice — знімок на момент покупки) не змінюються.
    /// </summary>
    Task<CatalogSyncResult> ApplyCatalogSnapshotAsync(
        long courseId, string title, decimal price, bool isAvailable, DateTime occurredAt, CancellationToken ct = default);
}

/// <summary>Результат застосування події Каталогу до локальної копії курсу.</summary>
public enum CatalogSyncResult
{
    /// <summary>Подія старша за вже застосовану — рядок не змінено.</summary>
    Stale = 0,
    Updated = 1,
    Inserted = 2
}
