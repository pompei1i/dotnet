using Enrollments.Domain.Models;

namespace Enrollments.Dal.Interfaces;

/// <summary>
/// Репозиторій журналу статусів. CRUD успадковано від IGenericRepository,
/// тут — лише вибірка історії одного замовлення.
/// </summary>
public interface IOrderStatusHistoryRepository : IGenericRepository<OrderStatusHistoryEntry>
{
    /// <summary>Історія статусів замовлення в хронологічному порядку.</summary>
    Task<IReadOnlyList<OrderStatusHistoryEntry>> GetByOrderIdAsync(long orderId, CancellationToken ct = default);
}
