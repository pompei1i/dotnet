namespace Enrollments.Domain.Models;

/// <summary>Підсумок продажів курсу: скільки оплачених замовлень його містять і на яку суму.</summary>
public class CourseSales
{
    public long CourseId { get; set; }
    public string CourseTitle { get; set; } = "";
    public int OrdersCount { get; set; }
    public decimal Revenue { get; set; }
}
