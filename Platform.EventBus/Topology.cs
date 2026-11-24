using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Топологія RabbitMQ (Лаб. 08) оголошується в коді під час старту — декларативно й ідемпотентно:
/// повторне оголошення того самого exchange чи черги з тими самими параметрами безпечне.
/// Параметри вже створеної черги вручну в Management UI не змінюються — інакше наступний старт
/// завершиться конфліктом (PRECONDITION_FAILED).
///
/// <code>
/// catalog-events (topic, durable)
///   ├─ course.*        ──► enrollments.catalog-sync ──(nack / вичерпано спроби)──► catalog-events.dlx ──► enrollments.catalog-sync.dlq
///   └─ course.renamed  ──► reviews.catalog-sync     ──(nack / вичерпано спроби)──► catalog-events.dlx ──► reviews.catalog-sync.dlq
/// </code>
///
/// Dead letter exchange — direct, і кожна черга відправляє «мертві» повідомлення з власним routing key
/// (x-dead-letter-routing-key = ім'я черги). Так повідомлення з черги Замовлень потрапляє лише в DLQ
/// Замовлень, а не в DLQ усіх сервісів (як було б зі спільним fanout DLX).
/// </summary>
public static class Topology
{
    /// <summary>Exchange подій Каталогу. Routing key — "course.{подія}".</summary>
    public const string CatalogEvents = "catalog-events";

    public static string DeadLetterExchange(string exchange) => $"{exchange}.dlx";

    public static string DeadLetterQueue(string queue) => $"{queue}.dlq";

    /// <summary>Exchange подій і його DLX — оголошують і publisher, і consumers.</summary>
    public static async Task DeclareExchangeAsync(IChannel channel, string exchange, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange(exchange), ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
    }

    /// <summary>
    /// Черга сервісу-споживача: durable, із DLX; bindings за шаблонами routing key
    /// ("*" — одне слово, "#" — будь-яка кількість); DLQ прив'язана до DLX ключем з іменем черги.
    /// </summary>
    public static async Task DeclareQueueAsync(
        IChannel channel, string exchange, string queue, IEnumerable<string> bindingKeys, CancellationToken ct)
    {
        await DeclareExchangeAsync(channel, exchange, ct);

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange(exchange),
                ["x-dead-letter-routing-key"] = queue
            },
            cancellationToken: ct);

        foreach (var key in bindingKeys.Distinct())
            await channel.QueueBindAsync(queue, exchange, key, cancellationToken: ct);

        await channel.QueueDeclareAsync(DeadLetterQueue(queue), durable: true, exclusive: false, autoDelete: false,
            cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue(queue), DeadLetterExchange(exchange), routingKey: queue, cancellationToken: ct);
    }
}
