namespace Enrollments.Domain.Models;

/// <summary>
/// Корінь агрегату «Замовлення» (таблиця dbo.Orders) разом із реквізитами та позиціями.
/// </summary>
public class Order
{
    public long Id { get; set; }
    public long StudentId { get; set; }
    public string Status { get; set; } = OrderStatuses.Submitted;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Токен оптимістичної конкурентності (стовпець ROWVERSION).</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Платіжні реквізити (зв'язок 1:1 з dbo.OrderDetails).</summary>
    public OrderDetails? Details { get; set; }

    /// <summary>Позиції замовлення (dbo.OrderItems).</summary>
    public List<OrderItem> Items { get; } = [];
}
