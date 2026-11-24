namespace Platform.EventBus;

/// <summary>
/// Базова інтеграційна подія (Лаб. 08) — бізнес-факт у минулому часі, яким сервіс-власник ділиться з іншими.
/// <list type="bullet">
/// <item>record: незмінна, порівнюється за значенням;</item>
/// <item>EventId — для ідемпотентності: споживач запам'ятовує оброблені Id і пропускає повторну доставку;</item>
/// <item>OccurredAt — коли факт стався (UTC): за ним споживач не перезапише новіші дані подією, що прийшла пізніше;</item>
/// <item>CorrelationId — бізнес-трасування (Лаб. 05): той самий ідентифікатор, що й у HTTP-запиті, який спричинив подію;</item>
/// <item>SchemaVersion — версія формату: нові поля додаються, наявні не перейменовуються і не видаляються.</item>
/// </list>
/// Подія самодостатня: усе потрібне для обробки — в ній, споживач не звертається до власника по дані.
/// Публікатор і споживачі мають кожен свою копію класу події (як у Microsoft eShop): контракт між ними —
/// ім'я типу (BasicProperties.Type) і JSON-форма, а не спільна збірка.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public string? CorrelationId { get; init; }

    public int SchemaVersion { get; init; } = 1;
}
