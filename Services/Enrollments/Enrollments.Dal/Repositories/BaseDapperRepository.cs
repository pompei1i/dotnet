using Dapper;
using Enrollments.Dal.Interfaces;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal.Repositories;

/// <summary>
/// Спільний CRUD на Dapper, написаний один раз для всіх сутностей із ключем Id.
/// Конкретний репозиторій передає назву таблиці й перелік стовпців і додає лише свої запити.
/// </summary>
public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    private readonly string _selectByIdSql;
    private readonly string _selectAllSql;
    private readonly string _insertSql;
    private readonly string _deleteSql;

    /// <param name="table">Назва таблиці, наприклад "dbo.Orders".</param>
    /// <param name="selectColumns">Стовпці для читання; назви збігаються з властивостями моделі.</param>
    /// <param name="insertColumns">Стовпці для вставки; значення беруться з однойменних властивостей.</param>
    /// <param name="softDelete">true — таблиця має IsDeleted: видалення м'яке, видалені рядки не читаються.</param>
    protected BaseDapperRepository(
        SqlConnection connection,
        Func<SqlTransaction?> currentTransaction,
        string table,
        string selectColumns,
        IReadOnlyList<string> insertColumns,
        bool softDelete)
        : base(connection, currentTransaction)
    {
        // Назва таблиці та стовпців — константи з коду конкретного репозиторію, а не вхідні дані
        // користувача, тому їх підстановка в текст запиту безпечна. Усі значення передаються
        // лише параметрами (@Id та @<Стовпець>).
        var activeOnly = softDelete ? " AND IsDeleted = 0" : "";
        var insertParameters = string.Join(", ", insertColumns.Select(column => "@" + column));

        _selectByIdSql = $"SELECT {selectColumns} FROM {table} WHERE Id = @Id{activeOnly}";
        _selectAllSql = $"SELECT {selectColumns} FROM {table} WHERE 1 = 1{activeOnly} ORDER BY Id";
        _insertSql = $"INSERT INTO {table} ({string.Join(", ", insertColumns)}) OUTPUT INSERTED.Id VALUES ({insertParameters})";
        _deleteSql = softDelete
            ? $"UPDATE {table} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0"
            : $"DELETE FROM {table} WHERE Id = @Id";
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<T>(Command(_selectByIdSql, new { Id = id }, ct));
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return (await Connection.QueryAsync<T>(Command(_selectAllSql, null, ct))).AsList();
    }

    public virtual async Task<long> AddAsync(T entity, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        try
        {
            // Dapper бере значення параметрів @<Стовпець> з однойменних властивостей сутності
            return await Connection.ExecuteScalarAsync<long>(Command(_insertSql, entity, ct));
        }
        catch (SqlException ex) when (SqlExceptionTranslator.ToDomainException(ex) is { } domainException)
        {
            throw domainException;
        }
    }

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.ExecuteAsync(Command(_deleteSql, new { Id = id }, ct)) > 0;
    }
}
