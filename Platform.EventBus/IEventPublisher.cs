namespace Platform.EventBus;

/// <summary>Публікація інтеграційних подій. Викликається лише після успішного збереження змін у БД.</summary>
public interface IEventPublisher
{
    /// <summary>
    /// Серіалізує подію в JSON і публікує її в exchange з routing key. Повертається, коли брокер
    /// підтвердив прийом (publisher confirms) — повідомлення вже записано в durable-черги.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, string exchange, string routingKey, CancellationToken ct = default)
        where TEvent : IntegrationEvent;
}
