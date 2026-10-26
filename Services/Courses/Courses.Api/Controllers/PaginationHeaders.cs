using System.Globalization;
using Courses.Bll.Dtos;

namespace Courses.Api.Controllers;

/// <summary>
/// Метадані сторінки дублюються в заголовках відповіді — клієнт і кеш бачать їх без розбору тіла.
/// </summary>
public static class PaginationHeaders
{
    public static void AddPaginationHeaders<T>(this HttpResponse response, PagedResult<T> page)
    {
        response.Headers["X-Total-Count"] = page.TotalCount.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Total-Pages"] = page.TotalPages.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page"] = page.Page.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page-Size"] = page.PageSize.ToString(CultureInfo.InvariantCulture);
    }
}
