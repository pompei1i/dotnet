using Enrollments.Bll.Dtos;

namespace Enrollments.Bll.Services;

public interface IOrderService
{
    Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default);

    Task<PagedResultDto<OrderSummaryDto>> GetByStudentAsync(
        long studentId, string? status, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<OrderStatusHistoryDto>> GetHistoryAsync(long id, CancellationToken ct = default);

    /// <summary>Створює замовлення з позиціями однією транзакцією.</summary>
    Task<OrderDto> CreateAsync(CreateOrderDto input, CancellationToken ct = default);

    /// <summary>Додає курс до замовлення у статусі Submitted однією транзакцією.</summary>
    Task<OrderDto> AddItemAsync(long orderId, CreateOrderItemDto input, CancellationToken ct = default);

    /// <summary>Переводить замовлення в новий статус, якщо перехід допустимий.</summary>
    Task ChangeStatusAsync(long orderId, string newStatus, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
