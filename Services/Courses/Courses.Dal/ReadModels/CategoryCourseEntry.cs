using Courses.Domain.Entities;

namespace Courses.Dal.ReadModels;

/// <summary>Курс у категорії разом із власними полями проміжної сутності — AddedAt та IsPrimary.</summary>
public record CategoryCourseEntry(
    long CourseId,
    string Title,
    string Slug,
    decimal Price,
    CourseLevel Level,
    bool IsPublished,
    DateTime AddedAt,
    bool IsPrimary);
