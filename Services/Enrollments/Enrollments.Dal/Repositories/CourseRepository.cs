using Dapper;
using Enrollments.Dal.Interfaces;
using Enrollments.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal.Repositories;

/// <summary>
/// Репозиторій локальної копії курсів на Dapper. Спільний CRUD успадковано від
/// BaseDapperRepository; тут — пошук із пагінацією (QueryMultiple) та агрегація продажів.
/// </summary>
public sealed class CourseRepository : BaseDapperRepository<Course>, ICourseRepository
{
    public CourseRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(
            connection,
            currentTransaction,
            table: "dbo.Courses",
            selectColumns: "Id, Title, Price, IsAvailable, SyncedAt",
            insertColumns: ["Id", "Title", "Price", "IsAvailable"],   // Id приходить із каталогу, без IDENTITY
            softDelete: true)
    { }

    public async Task<PagedResult<Course>> GetAvailableAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        // Два запити за одне звернення до сервера: загальна кількість і сама сторінка.
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Courses
            WHERE IsAvailable = 1 AND IsDeleted = 0
              AND (@Search IS NULL OR Title LIKE CONCAT(N'%', @Search, N'%'));

            SELECT Id, Title, Price, IsAvailable, SyncedAt
            FROM dbo.Courses
            WHERE IsAvailable = 1 AND IsDeleted = 0
              AND (@Search IS NULL OR Title LIKE CONCAT(N'%', @Search, N'%'))
            ORDER BY Title, Id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await EnsureOpenAsync(ct);

        using var grid = await Connection.QueryMultipleAsync(Command(
            sql, new { Search = search, Offset = (page - 1) * pageSize, PageSize = pageSize }, ct));

        var totalCount = await grid.ReadSingleAsync<int>();
        var courses = (await grid.ReadAsync<Course>()).AsList();

        return new PagedResult<Course>(courses, totalCount, page, pageSize);
    }

    public async Task<CourseSales?> GetSalesAsync(long courseId, CancellationToken ct = default)
    {
        // Враховуються лише оплачені замовлення; сума рахується за знімком ціни в позиції,
        // а не за поточною ціною курсу. Пошук позицій іде по індексу IX_OrderItems_CourseId.
        const string sql = """
            SELECT c.Id AS CourseId,
                   c.Title AS CourseTitle,
                   COUNT(o.Id) AS OrdersCount,
                   COALESCE(SUM(CASE WHEN o.Id IS NOT NULL THEN i.UnitPrice - i.Discount END), 0) AS Revenue
            FROM dbo.Courses AS c
            LEFT JOIN dbo.OrderItems AS i ON i.CourseId = c.Id
            LEFT JOIN dbo.Orders AS o
                   ON o.Id = i.OrderId
                  AND o.IsDeleted = 0
                  AND o.Status IN (N'Paid', N'AccessGranted')
            WHERE c.Id = @CourseId AND c.IsDeleted = 0
            GROUP BY c.Id, c.Title
            """;

        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<CourseSales>(
            Command(sql, new { CourseId = courseId }, ct));
    }
}
