using Enrollments.Domain.Models;

namespace Enrollments.Dal.Interfaces;

/// <summary>
/// Репозиторій агрегату «Замовлення». CRUD успадковано від IGenericRepository,
/// тут — лише специфічні запити: JOIN, блокування рядка, виклики процедур з Лаб. 01.
/// </summary>
public interface IOrderRepository : IGenericRepository<Order>
{
    /// <summary>Замовлення з реквізитами та позиціями одним запитом (multi-mapping).</summary>
    Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default);

    /// <summary>Читає рядок замовлення і блокує його до кінця поточної транзакції (UPDLOCK).</summary>
    Task<Order?> GetForUpdateAsync(long orderId, CancellationToken ct = default);

    /// <summary>Сторінка замовлень студента — процедура dbo.usp_GetStudentOrders.</summary>
    Task<PagedResult<OrderSummary>> GetByStudentAsync(
        long studentId, string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Кількість незавершених замовлень студента (Submitted, AwaitingPayment, Paid).</summary>
    Task<int> CountActiveByStudentAsync(long studentId, CancellationToken ct = default);

    Task AddDetailsAsync(OrderDetails details, CancellationToken ct = default);

    Task AddItemAsync(OrderItem item, CancellationToken ct = default);

    Task UpdateTotalAsync(long orderId, decimal totalAmount, string? updatedBy, CancellationToken ct = default);

    /// <summary>
    /// Транзакційна зміна статусу — процедура dbo.usp_ChangeOrderStatus.
    /// Недопустимий перехід завершується BusinessConflictException.
    /// </summary>
    Task ChangeStatusAsync(
        long orderId, string newStatus, string? changedBy, string? comment,
        byte[]? expectedRowVersion = null, CancellationToken ct = default);
}
