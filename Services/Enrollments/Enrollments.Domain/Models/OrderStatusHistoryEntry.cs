namespace Enrollments.Domain.Models;

/// <summary>Запис журналу зміни статусів. Відповідає таблиці dbo.OrderStatusHistory.</summary>
public class OrderStatusHistoryEntry
{
    public long Id { get; set; }
    public long OrderId { get; set; }

    /// <summary>null для запису про створення замовлення.</summary>
    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = "";
    public DateTime ChangedAt { get; set; }
    public string? ChangedBy { get; set; }
    public string? Comment { get; set; }
}
