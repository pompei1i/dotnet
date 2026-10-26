using System.ComponentModel.DataAnnotations;

namespace Courses.Bll.Dtos;

/// <summary>
/// Параметри списку курсів із рядка запиту: сторінка, фільтри, сортування.
/// Прості межі — атрибутами; допустимі поля сортування й узгодженість фільтрів
/// перевіряє CourseQueryValidator (FluentValidation).
/// </summary>
public class CourseQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Номер сторінки, починаючи з 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Частина назви курсу.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Beginner, Intermediate або Advanced.</summary>
    public string? Level { get; set; }

    public int? CategoryId { get; set; }

    public long? InstructorId { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public bool? IsPublished { get; set; }

    /// <summary>title, price, publishedAt, rating або createdAt.</summary>
    public string SortBy { get; set; } = "title";

    /// <summary>asc або desc.</summary>
    public string SortDir { get; set; } = "asc";
}
