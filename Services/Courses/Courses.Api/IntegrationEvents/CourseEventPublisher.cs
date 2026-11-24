using Courses.Api.IntegrationEvents.Events;
using Courses.Bll.Events;
using Platform.EventBus;
using Platform.ServiceDefaults;

namespace Courses.Api.IntegrationEvents;

/// <summary>
/// Реалізація ICourseEventPublisher на RabbitMQ (Лаб. 08): BLL каже «ціну змінено», а тут це стає
/// інтеграційною подією з CorrelationId поточного HTTP-запиту і публікується в exchange catalog-events.
///
/// Межа надійності: CourseService викликає цей клас уже ПІСЛЯ збереження в БД. Якщо брокер недоступний,
/// зміна лишається збереженою, а подію буде втрачено — тому збій логується як Error з усіма даними події
/// (для ручної повторної публікації), а клієнт отримує успіх: його зміна справді збережена. Гарантію
/// доставки дав би Transactional Outbox (подія в таблиці тієї самої БД, фоновий publisher) — див. README.
/// </summary>
public sealed class CourseEventPublisher(
    IEventPublisher publisher,
    IHttpContextAccessor http,
    ILogger<CourseEventPublisher> logger) : ICourseEventPublisher
{
    public Task PriceChangedAsync(CourseSnapshot course, decimal oldPrice, CancellationToken ct)
        => PublishAsync(new CoursePriceChangedIntegrationEvent(course.Id, course.Title, oldPrice, course.Price, course.IsPublished)
        {
            CorrelationId = CorrelationId
        }, CatalogRoutingKeys.PriceChanged, ct);

    public Task RenamedAsync(CourseSnapshot course, string oldTitle, CancellationToken ct)
        => PublishAsync(new CourseRenamedIntegrationEvent(course.Id, oldTitle, course.Title, course.Price, course.IsPublished)
        {
            CorrelationId = CorrelationId
        }, CatalogRoutingKeys.Renamed, ct);

    public Task PublicationChangedAsync(CourseSnapshot course, CancellationToken ct)
        => PublishAsync(new CoursePublicationChangedIntegrationEvent(course.Id, course.Title, course.Price, course.IsPublished)
        {
            CorrelationId = CorrelationId
        }, course.IsPublished ? CatalogRoutingKeys.Published : CatalogRoutingKeys.Unpublished, ct);

    private string? CorrelationId => http.HttpContext?.Items[CorrelationIdMiddleware.HeaderName] as string;

    private async Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken ct) where TEvent : IntegrationEvent
    {
        try
        {
            // Збереження вже відбулося — скасування HTTP-запиту не має зупиняти публікацію факту
            await publisher.PublishAsync(@event, Topology.CatalogEvents, routingKey, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Integration event lost: {EventType} {EventId} {@Event} was saved in CoursesDb but not published to {Exchange}/{RoutingKey}",
                typeof(TEvent).Name, @event.EventId, @event, Topology.CatalogEvents, routingKey);
        }
    }
}
