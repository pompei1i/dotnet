namespace Courses.Bll.Events;

/// <summary>
/// Публікація фактів про зміну курсу для інших сервісів (Лаб. 08). BLL знає лише цю абстракцію; реалізація
/// на RabbitMQ — Courses.Api/IntegrationEvents/CourseEventPublisher. Викликається ТІЛЬКИ після успішного
/// SaveChangesAsync: якщо транзакція БД не вдалася, виняток вилітає раніше, і подія не публікується.
/// </summary>
public interface ICourseEventPublisher
{
    Task PriceChangedAsync(CourseSnapshot course, decimal oldPrice, CancellationToken ct);

    Task RenamedAsync(CourseSnapshot course, string oldTitle, CancellationToken ct);

    Task PublicationChangedAsync(CourseSnapshot course, CancellationToken ct);
}

/// <summary>
/// Поточний стан курсу, який іде в кожну подію: споживач оновлює свою копію цілком і не звертається
/// до Каталогу по додаткові дані, а порядок подій розв'язує час події (OccurredAt).
/// </summary>
public sealed record CourseSnapshot(long Id, string Title, decimal Price, bool IsPublished);
