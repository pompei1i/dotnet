using Courses.Bll.Dtos;
using FluentValidation;

namespace Courses.Bll.Validation;

public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
{
    public CreateCategoryDtoValidator()
    {
        RuleFor(category => category.Slug)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug may contain only lowercase latin letters, digits and single hyphens");

        RuleFor(category => category.Name)
            .Must(name => name.Trim() == name)
            .WithMessage("Name must not start or end with spaces");
    }
}
