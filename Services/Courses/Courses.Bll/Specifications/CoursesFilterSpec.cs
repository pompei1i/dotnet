using Ardalis.Specification;
using Courses.Bll.Dtos;
using Courses.Domain.Entities;

namespace Courses.Bll.Specifications;

/// <summary>
/// Специфікація списку каталогу: фільтри, Include, сортування й сторінка описані тут, а не
/// в сервісі чи репозиторії. Вона не залежить від EF Core, тому її можна перевірити в пам'яті.
/// Репозиторій застосовує її через WithSpecification, і все виконується одним SQL-запитом;
/// для лічильника береться лише частина Where.
/// </summary>
public sealed class CoursesFilterSpec : Specification<Course>
{
    /// <summary>Поля, за якими дозволено сортувати; будь-що інше відхиляє CourseQueryValidator.</summary>
    public static readonly IReadOnlySet<string> SortFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "title", "price", "publishedAt", "rating", "createdAt" };

    public CoursesFilterSpec(CourseQuery query, int page, int pageSize)
    {
        Query.AsNoTracking();
        Query.Include(course => course.Instructor);
        Query.Include(course => course.CourseCategories).ThenInclude(link => link.Category);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            Query.Where(course => course.Title.Contains(search));
        }

        if (Enum.TryParse<CourseLevel>(query.Level, ignoreCase: true, out var level))
            Query.Where(course => course.Level == level);

        if (query.CategoryId is int categoryId)
            Query.Where(course => course.CourseCategories.Any(link => link.CategoryId == categoryId));

        if (query.InstructorId is long instructorId)
            Query.Where(course => course.InstructorId == instructorId);

        if (query.MinPrice is decimal minPrice)
            Query.Where(course => course.Price >= minPrice);

        if (query.MaxPrice is decimal maxPrice)
            Query.Where(course => course.Price <= maxPrice);

        if (query.IsPublished is bool isPublished)
            Query.Where(course => course.IsPublished == isPublished);

        ApplySorting(query.SortBy, string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase));

        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }

    /// <summary>Лише дозволені поля; Id другим ключем робить порядок стабільним між сторінками.</summary>
    private void ApplySorting(string? sortBy, bool descending)
    {
        var ordered = sortBy?.ToLowerInvariant() switch
        {
            "price" => descending ? Query.OrderByDescending(course => course.Price) : Query.OrderBy(course => course.Price),
            "publishedat" => descending ? Query.OrderByDescending(course => course.PublishedAt) : Query.OrderBy(course => course.PublishedAt),
            "rating" => descending ? Query.OrderByDescending(course => course.AverageRating) : Query.OrderBy(course => course.AverageRating),
            "createdat" => descending ? Query.OrderByDescending(course => course.CreatedAt) : Query.OrderBy(course => course.CreatedAt),
            _ => descending ? Query.OrderByDescending(course => course.Title) : Query.OrderBy(course => course.Title)
        };

        ordered.ThenBy(course => course.Id);
    }
}
