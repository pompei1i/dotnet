using Enrollments.Dal.Interfaces;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal.Repositories;

/// <summary>
/// INSERT у dbo.ProcessedEvents в транзакції Unit of Work. Унікальність гарантує PRIMARY KEY,
/// а не попередній SELECT: дві паралельні доставки однієї події не пройдуть обидві.
/// </summary>
public sealed class ProcessedEventRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    : RepositoryBase(connection, currentTransaction), IProcessedEventRepository
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<bool> TryAddAsync(Guid eventId, string eventType, string consumer, DateTime occurredAt, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.ProcessedEvents (EventId, EventType, Consumer, OccurredAt)
            VALUES (@EventId, @EventType, @Consumer, @OccurredAt)
            """;

        await EnsureOpenAsync(ct);
        try
        {
            await Dapper.SqlMapper.ExecuteAsync(Connection,
                Command(sql, new { EventId = eventId, EventType = eventType, Consumer = consumer, OccurredAt = occurredAt }, ct));
            return true;
        }
        catch (SqlException exception) when (exception.Number is PrimaryKeyViolation or UniqueIndexViolation)
        {
            return false;
        }
    }
}
