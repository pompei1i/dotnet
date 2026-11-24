using Enrollments.Dal;
using Enrollments.Dal.Interfaces;
using Platform.EventBus;

namespace Enrollments.Api.IntegrationEvents;

/// <summary>
/// Ідемпотентне оновлення локальної копії довідника курсів (dbo.Courses) за подією Каталогу (Лаб. 08) —
/// політика дублювання Лаб. 01: Title, Price, IsAvailable наздоганяють Каталог; OrderItems.UnitPrice і
/// CourseTitle в оформлених замовленнях — знімок на момент покупки й НЕ змінюються.
///
/// Одна транзакція Unit of Work:
/// 1. INSERT EventId у dbo.ProcessedEvents — порушення PRIMARY KEY означає повторну доставку: rollback, пропуск;
/// 2. оновлення рядка курсу, лише якщо подія новіша за SyncedAt (out-of-order не перезапише свіжі дані);
/// 3. commit — EventId і бізнес-зміна фіксуються разом або не фіксуються взагалі.
/// Виняток (база недоступна) відкочує все й вилітає далі — споживач повторить спробу, а потім відправить у DLQ.
/// </summary>
public sealed class CatalogCopyUpdater(IUnitOfWork uow, ILogger<CatalogCopyUpdater> logger)
{
    public const string Consumer = "enrollments.catalog-sync";

    public async Task ApplyAsync(IntegrationEvent @event, long courseId, string title, decimal price, bool isAvailable, CancellationToken ct)
    {
        var eventType = @event.GetType().Name;

        if (price < 0 || string.IsNullOrWhiteSpace(title))
            throw new PermanentEventException($"{eventType} {@event.EventId}: invalid course snapshot (title '{title}', price {price})");

        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, eventType, Consumer, @event.OccurredAt, ct))
            {
                await uow.RollbackAsync(ct);
                logger.LogInformation("Duplicate {EventType} {EventId} for course {CourseId} skipped: already processed",
                    eventType, @event.EventId, courseId);
                return;
            }

            var result = await uow.Courses.ApplyCatalogSnapshotAsync(courseId, title.Trim(), price, isAvailable, @event.OccurredAt, ct);
            await uow.CommitAsync(ct);

            if (result == CatalogSyncResult.Stale)
                logger.LogWarning("{EventType} {EventId} for course {CourseId} is older than the applied state ({OccurredAt}); local copy unchanged",
                    eventType, @event.EventId, courseId, @event.OccurredAt);
            else
                logger.LogInformation("Course {CourseId} local copy {SyncResult} from {EventType} {EventId}: title '{Title}', price {Price}, available {IsAvailable}",
                    courseId, result, eventType, @event.EventId, title, price, isAvailable);
        }
        catch (Exception exception)
        {
            try
            {
                await uow.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackFailure)
            {
                // Зв'язок із базою вже втрачено — транзакцію відкотить сам SQL Server; головне — вихідна причина
                logger.LogWarning("Rollback after {FailureType} failed: {RollbackFailure}", exception.GetType().Name, rollbackFailure.Message);
            }
            throw;
        }
    }
}
