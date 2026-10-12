using Dapper;
using Enrollments.Dal.Interfaces;
using Enrollments.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal.Repositories;

/// <summary>
/// Репозиторій журналу статусів на Dapper. Спільний CRUD успадковано від BaseDapperRepository.
/// Журнал лише дописується, тому м'якого видалення тут немає.
/// </summary>
public sealed class OrderStatusHistoryRepository
    : BaseDapperRepository<OrderStatusHistoryEntry>, IOrderStatusHistoryRepository
{
    public OrderStatusHistoryRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(
            connection,
            currentTransaction,
            table: "dbo.OrderStatusHistory",
            selectColumns: "Id, OrderId, OldStatus, NewStatus, ChangedAt, ChangedBy, Comment",
            insertColumns: ["OrderId", "OldStatus", "NewStatus", "ChangedBy", "Comment"],
            softDelete: false)
    { }

    public async Task<IReadOnlyList<OrderStatusHistoryEntry>> GetByOrderIdAsync(
        long orderId, CancellationToken ct = default)
    {
        // Працює по індексу IX_OrderStatusHistory_OrderId_ChangedAt
        const string sql = """
            SELECT Id, OrderId, OldStatus, NewStatus, ChangedAt, ChangedBy, Comment
            FROM dbo.OrderStatusHistory
            WHERE OrderId = @OrderId
            ORDER BY ChangedAt, Id
            """;

        await EnsureOpenAsync(ct);
        return (await Connection.QueryAsync<OrderStatusHistoryEntry>(
            Command(sql, new { OrderId = orderId }, ct))).AsList();
    }
}
