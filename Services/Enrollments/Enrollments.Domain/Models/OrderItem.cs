namespace Enrollments.Domain.Models;

/// <summary>Позиція замовлення. Відповідає таблиці dbo.OrderItems (M:N між Orders і Courses).</summary>
public class OrderItem
{
    public long OrderId { get; set; }
    public long CourseId { get; set; }

    /// <summary>Знімок назви курсу на момент оформлення — надалі не оновлюється.</summary>
    public string CourseTitle { get; set; } = "";

    /// <summary>Знімок ціни курсу на момент оформлення — надалі не оновлюється.</summary>
    public decimal UnitPrice { get; set; }

    public decimal Discount { get; set; }
    public string? CreatedBy { get; set; }

    public decimal LineTotal => UnitPrice - Discount;
}
