using System.Data;
using Dapper;
using Enrollments.Dal.Interfaces;
using Enrollments.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal.Repositories;

/// <summary>
/// Репозиторій агрегату «Замовлення» на Dapper. Спільний CRUD успадковано від
/// BaseDapperRepository; тут — multi-mapping, блокування рядка та виклики процедур з Лаб. 01.
/// </summary>
public sealed class OrderRepository : BaseDapperRepository<Order>, IOrderRepository
{
    public OrderRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(
            connection,
            currentTransaction,
            table: "dbo.Orders",
            selectColumns: "Id, StudentId, Status, OrderDate, TotalAmount, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion",
            insertColumns: ["StudentId", "Status", "TotalAmount", "CreatedBy"],
            softDelete: true)
    { }

    public async Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default)
    {
        // Один запит із двома JOIN: кожен рядок результату — це замовлення, його реквізити
        // та одна позиція. Dapper ділить рядок на три об'єкти за стовпцями з splitOn.
        const string sql = """
            SELECT o.Id, o.StudentId, o.Status, o.OrderDate, o.TotalAmount,
                   o.CreatedAt, o.CreatedBy, o.UpdatedAt, o.UpdatedBy, o.RowVersion,
                   d.OrderId, d.BillingName, d.BillingEmail, d.PaymentMethod, d.Comment,
                   i.CourseId, i.OrderId, i.CourseTitle, i.UnitPrice, i.Discount
            FROM dbo.Orders AS o
            LEFT JOIN dbo.OrderDetails AS d ON d.OrderId = o.Id
            LEFT JOIN dbo.OrderItems AS i ON i.OrderId = o.Id
            WHERE o.Id = @OrderId AND o.IsDeleted = 0
            ORDER BY i.CourseTitle
            """;

        await EnsureOpenAsync(ct);

        Order? result = null;
        await Connection.QueryAsync<Order, OrderDetails?, OrderItem?, Order>(
            Command(sql, new { OrderId = orderId }, ct),
            (order, details, item) =>
            {
                result ??= order;
                result.Details ??= details;
                if (item is not null) result.Items.Add(item);
                return result;
            },
            splitOn: "OrderId,CourseId");   // з OrderId починаються реквізити, з CourseId — позиція

        return result;
    }

    public async Task<Order?> GetForUpdateAsync(long orderId, CancellationToken ct = default)
    {
        // UPDLOCK тримає рядок замовлення до кінця транзакції Unit of Work: паралельний запит
        // не зможе змінити його статус чи суму між перевіркою та записом.
        const string sql = """
            SELECT Id, StudentId, Status, OrderDate, TotalAmount,
                   CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion
            FROM dbo.Orders WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @OrderId AND IsDeleted = 0
            """;

        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<Order>(Command(sql, new { OrderId = orderId }, ct));
    }

    public async Task<PagedResult<OrderSummary>> GetByStudentAsync(
        long studentId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        var rows = (await Connection.QueryAsync<OrderSummary>(Command(
            "dbo.usp_GetStudentOrders",
            new { StudentId = studentId, Status = status, PageNumber = page, PageSize = pageSize },
            ct,
            CommandType.StoredProcedure))).AsList();

        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
        return new PagedResult<OrderSummary>(rows, totalCount, page, pageSize);
    }

    public async Task<int> CountActiveByStudentAsync(long studentId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM dbo.Orders
            WHERE StudentId = @StudentId
              AND IsDeleted = 0
              AND Status IN (N'Submitted', N'AwaitingPayment', N'Paid')
            """;

        await EnsureOpenAsync(ct);
        return await Connection.ExecuteScalarAsync<int>(Command(sql, new { StudentId = studentId }, ct));
    }

    public async Task AddDetailsAsync(OrderDetails details, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.OrderDetails (OrderId, BillingName, BillingEmail, PaymentMethod, Comment, CreatedBy)
            VALUES (@OrderId, @BillingName, @BillingEmail, @PaymentMethod, @Comment, @CreatedBy)
            """;

        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(Command(sql, details, ct));
        }
        catch (SqlException ex) when (SqlExceptionTranslator.ToDomainException(ex) is { } domainException)
        {
            throw domainException;
        }
    }

    public async Task AddItemAsync(OrderItem item, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.OrderItems (OrderId, CourseId, CourseTitle, UnitPrice, Discount, CreatedBy)
            VALUES (@OrderId, @CourseId, @CourseTitle, @UnitPrice, @Discount, @CreatedBy)
            """;

        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(Command(sql, item, ct));
        }
        catch (SqlException ex) when (SqlExceptionTranslator.ToDomainException(ex) is { } domainException)
        {
            throw domainException;   // той самий курс двічі (PK_OrderItems) -> BusinessConflictException
        }
    }

    public async Task UpdateTotalAsync(
        long orderId, decimal totalAmount, string? updatedBy, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Orders
            SET TotalAmount = @TotalAmount, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
            WHERE Id = @OrderId AND IsDeleted = 0
            """;

        await EnsureOpenAsync(ct);
        await Connection.ExecuteAsync(Command(
            sql, new { OrderId = orderId, TotalAmount = totalAmount, UpdatedBy = updatedBy }, ct));
    }

    public async Task ChangeStatusAsync(
        long orderId, string newStatus, string? changedBy, string? comment,
        byte[]? expectedRowVersion = null, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            // Процедура сама працює в явній транзакції, перевіряє допустимість переходу
            // і пише в журнал статусів; недопустимий перехід — THROW 50003.
            await Connection.ExecuteAsync(Command(
                "dbo.usp_ChangeOrderStatus",
                new
                {
                    OrderId = orderId,
                    NewStatus = newStatus,
                    ChangedBy = changedBy,
                    Comment = comment,
                    ExpectedRowVersion = expectedRowVersion
                },
                ct,
                CommandType.StoredProcedure));
        }
        catch (SqlException ex) when (SqlExceptionTranslator.ToDomainException(ex) is { } domainException)
        {
            throw domainException;   // 50001 -> NotFound, 50003/50004/50009 -> BusinessConflict
        }
    }

    /// <summary>
    /// М'яке видалення через процедуру dbo.usp_DeleteOrder: на відміну від базового DeleteAsync,
    /// вона дозволяє видаляти лише чернетку (Submitted) або скасоване замовлення.
    /// </summary>
    public override async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(Command(
                "dbo.usp_DeleteOrder", new { OrderId = id }, ct, CommandType.StoredProcedure));
            return true;
        }
        catch (SqlException ex) when (SqlExceptionTranslator.ToDomainException(ex) is { } domainException)
        {
            throw domainException;   // 50001 -> NotFound, 50010 -> BusinessConflict
        }
    }
}
