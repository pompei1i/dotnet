using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Common;

/// <summary>Перевірки аргументів доменних методів; порушення — DomainException.</summary>
internal static class Guard
{
    public static string Text(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException($"{name} is required");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{name} must be at most {maxLength} characters");
        return trimmed;
    }

    public static string? OptionalText(string? value, string name, int maxLength)
        => string.IsNullOrWhiteSpace(value) ? null : Text(value, name, maxLength);

    public static long Id(long value, string name)
        => value > 0 ? value : throw new DomainException($"{name} must be positive");

    /// <summary>Список коротких рядків (переваги, теги): без порожніх елементів і дублікатів.</summary>
    public static List<string> Items(IEnumerable<string>? values, string name, int maxItems, int maxLength)
    {
        var items = (values ?? [])
            .Select(value => Text(value, name, maxLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (items.Count > maxItems)
            throw new DomainException($"At most {maxItems} {name} items are allowed");
        return items;
    }
}
