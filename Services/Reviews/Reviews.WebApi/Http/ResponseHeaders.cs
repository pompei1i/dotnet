using System.Globalization;
using Reviews.Application.Common.Models;

namespace Reviews.WebApi.Http;

/// <summary>Заголовки відповіді: версія документа в ETag і дані пагінації.</summary>
public static class ResponseHeaders
{
    /// <summary>ETag: "3" — версія документа; клієнт повертає її в If-Match під час зміни.</summary>
    public static void SetVersionETag(this HttpResponse response, int version)
        => response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Очікувана версія з If-Match: "3" або W/"3" -> 3; заголовка немає або "*" -> null (без перевірки).
    /// Некоректне значення -> 0, і валідатор команди поверне 400 з поясненням.
    /// </summary>
    public static int? ParseIfMatchVersion(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*")
            return null;

        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
            value = value[2..];

        return value.Length > 2 && value[0] == '"' && value[^1] == '"'
               && int.TryParse(value[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            ? version
            : 0;
    }

    public static void AddPagination<T>(this HttpResponse response, PagedResult<T> page)
    {
        response.Headers["X-Total-Count"] = page.TotalCount.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Total-Pages"] = page.TotalPages.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page"] = page.Page.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page-Size"] = page.PageSize.ToString(CultureInfo.InvariantCulture);
    }
}
