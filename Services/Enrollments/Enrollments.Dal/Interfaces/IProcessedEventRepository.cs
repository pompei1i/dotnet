namespace Enrollments.Dal.Interfaces;

/// <summary>Оброблені інтеграційні події (dbo.ProcessedEvents, міграція 001) — для ідемпотентності споживачів.</summary>
public interface IProcessedEventRepository
{
    /// <summary>
    /// Записує EventId як оброблений у поточній транзакції Unit of Work.
    /// false — подію вже оброблено (порушення PRIMARY KEY), бізнес-зміни треба пропустити.
    /// </summary>
    Task<bool> TryAddAsync(Guid eventId, string eventType, string consumer, DateTime occurredAt, CancellationToken ct = default);
}
