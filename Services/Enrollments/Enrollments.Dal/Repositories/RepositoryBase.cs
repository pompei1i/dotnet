using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal.Repositories;

/// <summary>
/// Спільна база всіх репозиторіїв. Репозиторій отримує не рядок підключення, а з'єднання
/// Unit of Work і доступ до його поточної транзакції, тому всі репозиторії однієї
/// бізнес-операції працюють через одне з'єднання та одну транзакцію.
/// З'єднання належить Unit of Work: саме він його звільняє у DisposeAsync.
/// </summary>
public abstract class RepositoryBase
{
    /// <summary>
    /// Таймаут однієї команди. 30 секунд — типове значення SqlClient: коротші запити цього
    /// сервісу вкладаються з великим запасом, а завислий запит не тримає блокування безкінечно.
    /// </summary>
    protected const int CommandTimeoutSeconds = 30;

    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    /// <summary>Поточна транзакція Unit of Work (або null, якщо її не розпочато).</summary>
    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }

    /// <summary>Команда Dapper у поточній транзакції, з таймаутом і токеном скасування.</summary>
    protected CommandDefinition Command(
        string sql, object? parameters, CancellationToken ct, CommandType commandType = CommandType.Text)
        => new(sql, parameters, Transaction, CommandTimeoutSeconds, commandType, cancellationToken: ct);
}
