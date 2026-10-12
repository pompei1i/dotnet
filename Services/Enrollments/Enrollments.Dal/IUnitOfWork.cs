using System.Data;
using Enrollments.Dal.Interfaces;

namespace Enrollments.Dal;

/// <summary>
/// Одиниця роботи: одне з'єднання та одна транзакція для всіх репозиторіїв бізнес-операції.
/// Реєструється в DI як Scoped — один екземпляр і одне з'єднання на HTTP-запит.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IStudentRepository Students { get; }
    IOrderRepository Orders { get; }
    ICourseRepository Courses { get; }
    IOrderStatusHistoryRepository StatusHistory { get; }

    Task BeginTransactionAsync(
        IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);
}
