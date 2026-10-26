using Courses.Bll.Dtos;
using Courses.Bll.Specifications;
using Courses.Domain.Entities;
using FluentValidation;

namespace Courses.Bll.Validation;

/// <summary>
/// Параметри списку: сортувати можна лише за дозволеними полями, ціновий діапазон має бути
/// узгодженим. Межі Page і PageSize перевіряють атрибути на CourseQuery.
/// </summary>
public class CourseQueryValidator : AbstractValidator<CourseQuery>
{
    public CourseQueryValidator()
    {
        RuleFor(query => query.SortBy)
            .Must(field => CoursesFilterSpec.SortFields.Contains(field))
            .WithMessage($"SortBy must be one of: {string.Join(", ", CoursesFilterSpec.SortFields)}");

        RuleFor(query => query.SortDir)
            .Must(direction => string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDir must be asc or desc");

        RuleFor(query => query.Level)
            .IsEnumName(typeof(CourseLevel), caseSensitive: false)
            .When(query => query.Level is not null)
            .WithMessage("Level must be Beginner, Intermediate or Advanced");

        RuleFor(query => query.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(query => query.MinPrice is not null);

        RuleFor(query => query.MaxPrice)
            .GreaterThanOrEqualTo(query => query.MinPrice!.Value)
            .When(query => query.MinPrice is not null && query.MaxPrice is not null)
            .WithMessage("MaxPrice must not be less than MinPrice");
    }
}
