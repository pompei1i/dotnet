namespace Platform.EventBus;

/// <summary>Обробник одного типу подій. Створюється в DI-scope повідомлення (Scoped-залежності — свої на кожне).</summary>
public interface IIntegrationEventHandler<in TEvent> : IIntegrationEventHandler
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);

    Task IIntegrationEventHandler.HandleAsync(IntegrationEvent @event, CancellationToken ct) => HandleAsync((TEvent)@event, ct);
}

/// <summary>Нетипізована форма — для диспетчеризації за типом із BasicProperties.Type.</summary>
public interface IIntegrationEventHandler
{
    Task HandleAsync(IntegrationEvent @event, CancellationToken ct);
}

/// <summary>
/// Постійний збій обробки: дані події некоректні або несумісні, від повтору нічого не зміниться.
/// Такі повідомлення одразу йдуть у DLQ, без повторів.
/// </summary>
public sealed class PermanentEventException(string message, Exception? inner = null) : Exception(message, inner);
