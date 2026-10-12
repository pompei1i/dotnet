namespace Enrollments.Domain.Models;

/// <summary>Рядок списку замовлень студента — результат процедури dbo.usp_GetStudentOrders.</summary>
public class OrderSummary
{
    public long Id { get; set; }
    public string Status { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Загальна кількість замовлень за фільтром; однакова в кожному рядку сторінки.</summary>
    public int TotalCount { get; set; }
}
