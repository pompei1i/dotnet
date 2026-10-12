namespace Enrollments.Domain.Models;

/// <summary>
/// Локальна копія курсу з каталогу (таблиця dbo.Courses). Власник даних — сервіс Courses:
/// цей сервіс курс лише читає, а оновлюватимуть копію події з каталогу (Лаб. 08).
/// </summary>
public class Course
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime SyncedAt { get; set; }
}
