using Platform.EventBus;

namespace Reviews.WebApi.IntegrationEvents.Events;

/// <summary>
/// Власна копія контракту події Каталогу: сервісу відгуків з усіх подій курсу потрібне лише перейменування
/// (binding "course.renamed"), а з полів — Id курсу й нова назва. Інші поля JSON ігноруються.
/// </summary>
public sealed record CourseRenamedIntegrationEvent(long CourseId, string OldTitle, string NewTitle) : IntegrationEvent;
