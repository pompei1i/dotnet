using System.ComponentModel.DataAnnotations;

namespace Courses.Bll.Dtos;

public record CategoryDto(int Id, string Name, string Slug, string? Description, int CoursesCount);

/// <summary>Категорія з її курсами (Explicit Loading).</summary>
public record CategoryWithCoursesDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    IReadOnlyList<CategoryCourseDto> Courses);

/// <summary>Курс у категорії разом із власними полями проміжної сутності.</summary>
public record CategoryCourseDto(
    long CourseId,
    string Title,
    string Slug,
    decimal Price,
    string Level,
    bool IsPublished,
    string InstructorName,
    DateTime AddedAt,
    bool IsPrimary);

/// <summary>Курс, нещодавно доданий у категорію (LINQ-запит через проміжну сутність).</summary>
public record RecentCourseDto(
    long CourseId,
    string Title,
    string Slug,
    decimal Price,
    string Level,
    bool IsPublished,
    DateTime AddedAt,
    bool IsPrimary);

public class CreateCategoryDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required, StringLength(100)]
    public string Slug { get; set; } = "";

    [StringLength(500)]
    public string? Description { get; set; }
}
