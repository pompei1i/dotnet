using System.ComponentModel.DataAnnotations;

namespace Courses.Bll.Dtos;

/// <summary>Курс у списку каталогу.</summary>
public record CourseDto(
    long Id,
    string Title,
    string Slug,
    decimal Price,
    string Level,
    string Language,
    bool IsPublished,
    DateTime? PublishedAt,
    decimal? AverageRating,
    int ReviewsCount,
    long InstructorId,
    string InstructorName,
    IReadOnlyList<string> Categories);

/// <summary>Картка курсу з усім пов'язаним.</summary>
public record CourseDetailsDto(
    long Id,
    string Title,
    string Slug,
    decimal Price,
    string Level,
    string Language,
    bool IsPublished,
    DateTime? PublishedAt,
    decimal? AverageRating,
    int ReviewsCount,
    InstructorSummaryDto Instructor,
    CourseDetailDto? Detail,
    IReadOnlyList<LessonDto> Lessons,
    IReadOnlyList<CourseCategoryDto> Categories);

public record InstructorSummaryDto(long Id, string FullName);

public record CourseDetailDto(string Description, string? Requirements, decimal DurationHours, bool HasCertificate);

public record LessonDto(long Id, string Title, int OrderIndex, int DurationMinutes, bool IsFreePreview);

public record CourseCategoryDto(int CategoryId, string Name, string Slug, bool IsPrimary, DateTime AddedAt);

public class CreateCourseDto
{
    [Range(1, long.MaxValue)]
    public long InstructorId { get; set; }

    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = "";

    /// <summary>Адреса сторінки курсу: малі латинські літери, цифри та дефіси.</summary>
    [Required, StringLength(200)]
    public string Slug { get; set; } = "";

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    /// <summary>Beginner, Intermediate або Advanced.</summary>
    [Required]
    public string Level { get; set; } = "Beginner";

    [StringLength(10, MinimumLength = 2)]
    public string Language { get; set; } = "uk";

    [Required, MinLength(10)]
    public string Description { get; set; } = "";

    [StringLength(2000)]
    public string? Requirements { get; set; }

    [Range(0.1, 9999.9)]
    public decimal DurationHours { get; set; }

    public bool HasCertificate { get; set; }

    [Required, MinLength(1), MaxLength(5)]
    public List<int> CategoryIds { get; set; } = [];

    /// <summary>Основна категорія; має бути серед CategoryIds. Не вказана — основною стає перша.</summary>
    public int? PrimaryCategoryId { get; set; }
}

public class ChangePriceDto
{
    [Range(0, 1_000_000)]
    public decimal Price { get; set; }
}

public class CreateLessonDto
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [Range(1, 600)]
    public int DurationMinutes { get; set; }

    public bool IsFreePreview { get; set; }
}

public class AssignCategoryDto
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public bool IsPrimary { get; set; }
}
