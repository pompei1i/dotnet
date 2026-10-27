using Courses.Bll.Dtos;
using FluentValidation;

namespace Courses.Bll.Validation;

/// <summary>Діапазон ціни задає атрибут; тут — лише точність, яку зберігає decimal(18,2).</summary>
public class ChangePriceDtoValidator : AbstractValidator<ChangePriceDto>
{
    public ChangePriceDtoValidator()
    {
        RuleFor(input => input.Price)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Price may have at most two decimal places");
    }
}
