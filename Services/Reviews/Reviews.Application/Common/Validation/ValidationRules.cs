using FluentValidation;
using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Application.Common.Validation;

/// <summary>Спільні правила FluentValidation, зокрема MongoDB-специфічні.</summary>
public static class ValidationRules
{
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    /// <summary>Рядок має бути коректним ObjectId (24 шістнадцяткові символи).</summary>
    public static IRuleBuilderOptions<T, string?> MustBeObjectId<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("{PropertyName} must be a valid ObjectId (24 hexadecimal characters)");

    /// <summary>Очікувана версія з If-Match, якщо її передано, — ціле число від 1.</summary>
    public static IRuleBuilderOptions<T, int?> MustBeVersion<T>(this IRuleBuilder<T, int?> rule)
        => rule.Must(version => version is null or >= 1)
            .WithMessage("If-Match must contain a document version (an integer starting from 1)");

    /// <summary>Переваги, недоліки, теги: короткий список непорожніх рядків.</summary>
    public static IRuleBuilderOptions<T, IReadOnlyList<string>?> MustBeShortList<T>(
        this IRuleBuilder<T, IReadOnlyList<string>?> rule, int maxItems, int maxLength)
        => rule.Must(items => items is null
                || (items.Count <= maxItems && items.All(item => !string.IsNullOrWhiteSpace(item) && item.Trim().Length <= maxLength)))
            .WithMessage($"{{PropertyName}} may contain at most {maxItems} non-empty items of up to {maxLength} characters");

    public static IRuleBuilderOptions<T, string?> MustBeEnum<T, TEnum>(this IRuleBuilder<T, string?> rule)
        where TEnum : struct, Enum
        => rule.Must(EnumText.IsValid<TEnum>)
            .WithMessage($"{{PropertyName}} must be one of: {EnumText.Allowed<TEnum>()}");

    public static IRuleBuilderOptions<T, string> ValidCourseTitle<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().MaximumLength(Review.MaxCourseTitleLength);
}
