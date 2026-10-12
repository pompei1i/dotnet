namespace Enrollments.Domain.Models;

/// <summary>Платіжні реквізити замовлення. Відповідає таблиці dbo.OrderDetails (1:1 з Orders).</summary>
public class OrderDetails
{
    public long OrderId { get; set; }
    public string BillingName { get; set; } = "";
    public string BillingEmail { get; set; } = "";
    public string PaymentMethod { get; set; } = "Card";
    public string? Comment { get; set; }
    public string? CreatedBy { get; set; }
}
