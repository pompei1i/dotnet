using Platform.EventBus;

namespace Enrollments.Api.IntegrationEvents.Events;

// Власна копія контракту подій Каталогу (як у Microsoft eShop: кожен сервіс оголошує потрібні йому події сам).
// Контракт із Каталогом — ім'я типу (BasicProperties.Type) і JSON-поля; зайві поля нових версій ігноруються.

public sealed record CoursePriceChangedIntegrationEvent(
    long CourseId,
    string Title,
    decimal OldPrice,
    decimal NewPrice,
    bool IsPublished) : IntegrationEvent;

public sealed record CourseRenamedIntegrationEvent(
    long CourseId,
    string OldTitle,
    string NewTitle,
    decimal Price,
    bool IsPublished) : IntegrationEvent;

public sealed record CoursePublicationChangedIntegrationEvent(
    long CourseId,
    string Title,
    decimal Price,
    bool IsPublished) : IntegrationEvent;
