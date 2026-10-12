namespace Enrollments.Domain.Models;

/// <summary>Студент-покупець. Відповідає таблиці dbo.Students.</summary>
public class Student
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Токен оптимістичної конкурентності (стовпець ROWVERSION).</summary>
    public byte[] RowVersion { get; set; } = [];
}
