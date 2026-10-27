using System.Text.RegularExpressions;
using Courses.Domain.Exceptions;

namespace Courses.Domain;

/// <summary>Перевірки, які сутності виконують у конструкторах і методах.</summary>
internal static partial class Guard
{
    public static string Text(string? value, string name, int maxLength, int minLength = 1)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length < minLength)
            throw new DomainException($"{name} must contain at least {minLength} character(s)");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{name} must not exceed {maxLength} characters");
        return trimmed;
    }

    public static string? OptionalText(string? value, string name, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? null : Text(value, name, maxLength);

    public static string Slug(string? value, int maxLength)
    {
        var slug = Text(value, "Slug", maxLength);
        if (!SlugPattern().IsMatch(slug))
            throw new DomainException("Slug may contain only lowercase latin letters, digits and single hyphens");
        return slug;
    }

    public static string Email(string? value, int maxLength)
    {
        var email = Text(value, "Email", maxLength);
        if (!EmailPattern().IsMatch(email))
            throw new DomainException($"'{email}' is not a valid email address");
        return email;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    // Той самий шаблон, що й CHECK у базі: щось@щось.щось
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
