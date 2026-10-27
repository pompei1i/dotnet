using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Запускає всі валідатори FluentValidation для команди чи запиту, збирає всі помилки разом
/// і за їх наявності кидає ValidationException — обробник при цьому не викликається.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        // Кожен валідатор отримує власний контекст: спільний контекст накопичує помилки,
        // і результат другого валідатора містив би ще й помилки першого
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), ct);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(ct);
    }
}
