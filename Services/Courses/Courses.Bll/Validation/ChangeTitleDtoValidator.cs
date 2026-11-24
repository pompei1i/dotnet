using Courses.Bll.Dtos;
using FluentValidation;

namespace Courses.Bll.Validation;

/// <summary>Нова назва курсу (Лаб. 08): не з самих пробілів; довжину задає атрибут, решту правил — сутність.</summary>
public class ChangeTitleDtoValidator : AbstractValidator<ChangeTitleDto>
{
    public ChangeTitleDtoValidator()
    {
        RuleFor(input => input.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title) && title.Trim().Length >= 3)
            .WithMessage("Title must contain at least 3 non-space characters");
    }
}
