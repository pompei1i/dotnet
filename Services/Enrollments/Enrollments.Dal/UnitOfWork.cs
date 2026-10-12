using System.Data;
using Enrollments.Dal.Interfaces;
using Enrollments.Dal.Repositories;
using Microsoft.Data.SqlClient;

namespace Enrollments.Dal;

/// <summary>
/// Unit of Work на ADO.NET: відкриває ОДНЕ з'єднання на всю бізнес-операцію, починає транзакцію
/// з потрібним рівнем ізоляції та гарантує Commit або Rollback для всіх репозиторіїв разом.
///
/// Рівень ізоляції за замовчуванням — ReadCommitted: «брудних» читань немає, а блокування
/// читання знімаються одразу, тож паралельні запити майже не чекають один одного.
/// Вищі рівні (RepeatableRead, Serializable) прибирають більше аномалій, але тримають
/// блокування до кінця транзакції — зростає час очікування і ризик взаємних блокувань.
/// Тому там, де потрібно захистити «прочитав — перевірив — записав», цей сервіс не піднімає
/// рівень для всієї транзакції, а блокує один конкретний рядок (UPDLOCK в
/// OrderRepository.GetForUpdateAsync).
/// Таймаут команди — 30 секунд (RepositoryBase.CommandTimeoutSeconds).
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        // З'єднання береться з пулу SqlClient (увімкнений за замовчуванням) і відкривається
        // ліниво — під час першого запиту або початку транзакції.
        _connection = new SqlConnection(connectionString);

        // Усі репозиторії отримують ОДНЕ з'єднання і доступ до ПОТОЧНОЇ транзакції
        Students = new StudentRepository(_connection, () => _transaction);
        Orders = new OrderRepository(_connection, () => _transaction);
        Courses = new CourseRepository(_connection, () => _transaction);
        StatusHistory = new OrderStatusHistoryRepository(_connection, () => _transaction);
    }

    public IStudentRepository Students { get; }
    public IOrderRepository Orders { get; }
    public ICourseRepository Courses { get; }
    public IOrderStatusHistoryRepository StatusHistory { get; }

    public async Task BeginTransactionAsync(
        IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("Transaction has already been started");

        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);

        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Transaction has not been started");

        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;

        try
        {
            await _transaction.RollbackAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Викликається контейнером DI наприкінці HTTP-запиту. Незавершена транзакція
    /// відкочується під час Dispose, з'єднання повертається в пул.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();

        await _connection.DisposeAsync();
    }
}
