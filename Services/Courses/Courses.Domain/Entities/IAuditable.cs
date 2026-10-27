namespace Courses.Domain.Entities;

/// <summary>
/// Сутність з аудитними колонками. Значення виставляє CoursesDbContext під час збереження,
/// тому в самих сутностях вони мають лише приватні setter-и.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; }
    string? CreatedBy { get; }
    DateTime? UpdatedAt { get; }
    string? UpdatedBy { get; }
}
