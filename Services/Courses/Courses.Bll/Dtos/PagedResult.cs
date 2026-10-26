namespace Courses.Bll.Dtos;

/// <summary>
/// Сторінка колекції. Метадані повертаються в тілі відповіді й дублюються в заголовках
/// X-Total-Count, X-Total-Pages, X-Page, X-Page-Size (див. README).
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}
