using System.Data;
using Enrollments.Dal.Interfaces;
using Enrollments.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal.Repositories;

/// <summary>
/// Репозиторій на чистому ADO.NET: SqlCommand з типізованими параметрами та ручне читання
/// через SqlDataReader, без Dapper. Свідомо не успадковує BaseDapperRepository і не реалізує
/// IGenericRepository — показує, що саме мікро-ORM робить за розробника.
/// </summary>
public sealed class StudentRepository : RepositoryBase, IStudentRepository
{
    public StudentRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<Student?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Email, FullName, Phone, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion
            FROM dbo.Students
            WHERE Id = @Id AND IsDeleted = 0
            """;

        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;   // значення — лише параметром

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadStudent(reader) : null;
    }

    public async Task<Student?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Email, FullName, Phone, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion
            FROM dbo.Students
            WHERE Email = @Email AND IsDeleted = 0
            """;

        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email;

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadStudent(reader) : null;
    }

    public async Task<PagedResult<Student>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        // Шаблон LIKE збирає сам SQL Server із параметра @Search, тому апостроф чи інший
        // спецсимвол у пошуку (O'Brien) не може змінити текст запиту.
        const string sql = """
            SELECT Id, Email, FullName, Phone, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion,
                   COUNT(*) OVER () AS TotalCount
            FROM dbo.Students
            WHERE IsDeleted = 0
              AND (@Search IS NULL
                   OR FullName LIKE CONCAT(@Search, N'%')
                   OR Email LIKE CONCAT(@Search, N'%'))
            ORDER BY FullName, Id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = (object?)search ?? DBNull.Value;
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

        var students = new List<Student>();
        var totalCount = 0;

        await using var reader = await command.ExecuteReaderAsync(ct);
        var totalCountOrdinal = reader.GetOrdinal("TotalCount");
        while (await reader.ReadAsync(ct))
        {
            students.Add(ReadStudent(reader));
            totalCount = reader.GetInt32(totalCountOrdinal);
        }

        return new PagedResult<Student>(students, totalCount, page, pageSize);
    }

    public async Task<long> AddAsync(Student student, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Students (Email, FullName, Phone, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES (@Email, @FullName, @Phone, @CreatedBy)
            """;

        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = student.Email;
        command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = student.FullName;
        command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object?)student.Phone ?? DBNull.Value;
        command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = (object?)student.CreatedBy ?? DBNull.Value;

        try
        {
            return (long)(await command.ExecuteScalarAsync(ct))!;
        }
        catch (SqlException ex) when (SqlExceptionTranslator.ToDomainException(ex) is { } domainException)
        {
            throw domainException;   // дубль Email (UQ_Students_Email) -> BusinessConflictException
        }
    }

    public async Task<bool> UpdateAsync(Student student, CancellationToken ct = default)
    {
        // Оптимістична конкурентність: рядок оновлюється, лише якщо RowVersion той самий,
        // що був під час читання. Інакше змінено 0 рядків — конфлікт вирішує BLL.
        const string sql = """
            UPDATE dbo.Students
            SET Email = @Email,
                FullName = @FullName,
                Phone = @Phone,
                UpdatedAt = SYSUTCDATETIME(),
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0 AND RowVersion = @RowVersion
            """;

        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = student.Id;
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = student.Email;
        command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = student.FullName;
        command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object?)student.Phone ?? DBNull.Value;
        command.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = (object?)student.UpdatedBy ?? DBNull.Value;
        command.Parameters.Add("@RowVersion", SqlDbType.Binary, 8).Value = student.RowVersion;

        try
        {
            return await command.ExecuteNonQueryAsync(ct) == 1;
        }
        catch (SqlException ex) when (SqlExceptionTranslator.ToDomainException(ex) is { } domainException)
        {
            throw domainException;
        }
    }

    public async Task<bool> DeleteAsync(long id, string? deletedBy, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Students
            SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @DeletedBy
            WHERE Id = @Id AND IsDeleted = 0
            """;

        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@DeletedBy", SqlDbType.NVarChar, 100).Value = (object?)deletedBy ?? DBNull.Value;

        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    /// <summary>Команда на з'єднанні Unit of Work у його поточній транзакції.</summary>
    private SqlCommand CreateCommand(string sql)
        => new(sql, Connection, Transaction) { CommandTimeout = CommandTimeoutSeconds };

    /// <summary>Ручний мапінг одного рядка в модель — те, що Dapper робить автоматично.</summary>
    private static Student ReadStudent(SqlDataReader reader)
    {
        var phone = reader.GetOrdinal("Phone");
        var createdBy = reader.GetOrdinal("CreatedBy");
        var updatedAt = reader.GetOrdinal("UpdatedAt");
        var updatedBy = reader.GetOrdinal("UpdatedBy");

        return new Student
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            FullName = reader.GetString(reader.GetOrdinal("FullName")),
            Phone = reader.IsDBNull(phone) ? null : reader.GetString(phone),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            CreatedBy = reader.IsDBNull(createdBy) ? null : reader.GetString(createdBy),
            UpdatedAt = reader.IsDBNull(updatedAt) ? null : reader.GetDateTime(updatedAt),
            UpdatedBy = reader.IsDBNull(updatedBy) ? null : reader.GetString(updatedBy),
            RowVersion = (byte[])reader["RowVersion"]
        };
    }
}
