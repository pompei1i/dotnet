namespace Enrollments.Domain.Models;

/// <summary>Одна сторінка результатів разом із загальною кількістю записів.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
