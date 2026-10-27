using Courses.Bll.Dtos;
using Courses.Domain.Entities;
using FluentValidation;

namespace Courses.Bll.Validation;

/// <summary>
/// Правила, яких не виразити атрибутами на DTO: формат Slug, назва рівня, унікальність
/// і узгодженість переданих категорій. Обов'язковість і довжини лишаються на атрибутах —
/// правила не дублюються.
/// </summary>
public class CreateCourseDtoValidator : AbstractValidator<CreateCourseDto>
{
    public CreateCourseDtoValidator()
    {
        RuleFor(course => course.Slug)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug may contain only lowercase latin letters, digits and single hyphens, e.g. 'ef-core-in-practice'");

        RuleFor(course => course.Level)
            .IsEnumName(typeof(CourseLevel), caseSensitive: false)
            .WithMessage("Level must be Beginner, Intermediate or Advanced");

        RuleFor(course => course.Price)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Price may have at most two decimal places");

        RuleFor(course => course.CategoryIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Each category can be specified only once");

        RuleForEach(course => course.CategoryIds)
            .GreaterThan(0);

        RuleFor(course => course.PrimaryCategoryId)
            .Must((course, primaryId) => course.CategoryIds.Contains(primaryId!.Value))
            .When(course => course.PrimaryCategoryId is not null)
            .WithMessage("Primary category must be one of CategoryIds");
    }
}
