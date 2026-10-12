namespace Enrollments.Bll.Dtos;

/// <summary>Одна сторінка результатів разом із загальною кількістю записів.</summary>
public record PagedResultDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
