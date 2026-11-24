using Platform.EventBus;

namespace Courses.Api.IntegrationEvents.Events;

// Інтеграційні події Каталогу (Лаб. 08) — бізнес-факти в минулому часі, якими Каталог як власник даних
// курсу ділиться з іншими сервісами (політика дублювання Лаб. 01). Незмінні records; EventId, OccurredAt,
// CorrelationId і SchemaVersion — з базового IntegrationEvent. Кожна подія несе ПОВНИЙ поточний знімок
// курсу (Title, Price, IsPublished): споживач оновлює копію цілком і не питає Каталог, а порядок
// розв'язує OccurredAt. Нові поля додаються в кінець, наявні не перейменовуються (SchemaVersion).

/// <summary>Ціна курсу змінилася. Routing key "course.price_changed".</summary>
public sealed record CoursePriceChangedIntegrationEvent(
    long CourseId,
    string Title,
    decimal OldPrice,
    decimal NewPrice,
    bool IsPublished) : IntegrationEvent;

/// <summary>Курс перейменовано. Routing key "course.renamed".</summary>
public sealed record CourseRenamedIntegrationEvent(
    long CourseId,
    string OldTitle,
    string NewTitle,
    decimal Price,
    bool IsPublished) : IntegrationEvent;

/// <summary>Курс опубліковано або знято з публікації. Routing key "course.published" / "course.unpublished".</summary>
public sealed record CoursePublicationChangedIntegrationEvent(
    long CourseId,
    string Title,
    decimal Price,
    bool IsPublished) : IntegrationEvent;

/// <summary>Routing keys exchange catalog-events: "course.{що сталося}".</summary>
public static class CatalogRoutingKeys
{
    public const string PriceChanged = "course.price_changed";
    public const string Renamed = "course.renamed";
    public const string Published = "course.published";
    public const string Unpublished = "course.unpublished";
}
