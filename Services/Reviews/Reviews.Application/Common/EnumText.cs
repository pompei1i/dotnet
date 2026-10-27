namespace Reviews.Application.Common;

/// <summary>
/// Перерахування в документах і API — рядки в нижньому регістрі ("student", "open"),
/// як у документах Лаб. 01. Числові рядки ("1") не приймаються.
/// </summary>
public static class EnumText
{
    public static string From<TEnum>(TEnum value) where TEnum : struct, Enum
        => value.ToString().ToLowerInvariant();

    public static bool IsValid<TEnum>(string? text) where TEnum : struct, Enum
        => !string.IsNullOrWhiteSpace(text)
           && !char.IsDigit(text.Trim()[0])
           && Enum.TryParse<TEnum>(text, ignoreCase: true, out var value)
           && Enum.IsDefined(value);

    public static TEnum Parse<TEnum>(string text) where TEnum : struct, Enum
        => IsValid<TEnum>(text)
            ? Enum.Parse<TEnum>(text, ignoreCase: true)
            : throw new ArgumentException($"'{text}' is not a valid {typeof(TEnum).Name}", nameof(text));

    /// <summary>Перелік допустимих значень для повідомлень валідації.</summary>
    public static string Allowed<TEnum>() where TEnum : struct, Enum
        => string.Join(", ", Enum.GetValues<TEnum>().Select(From));
}
