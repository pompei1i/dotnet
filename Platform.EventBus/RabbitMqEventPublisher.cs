using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>
/// Publisher RabbitMQ (Лаб. 08). IConnection — один на застосунок (Aspire.RabbitMQ.Client), канал для
/// публікації — один на publisher, з publisher confirms: PublishAsync повертається лише тоді, коли брокер
/// записав повідомлення. Канал не потокобезпечний для паралельних публікацій, тож доступ до нього
/// послідовний (SemaphoreSlim); якщо канал закрився, наступна публікація відкриває новий.
///
/// Властивості повідомлення: Persistent (переживе перезапуск брокера в durable-черзі), ContentType JSON,
/// MessageId = EventId (ідемпотентність споживача), Type = ім'я класу події (маршрутизація до обробника),
/// Timestamp = OccurredAt, заголовки x-correlation-id і x-schema-version, а також traceparent.
/// </summary>
public sealed class RabbitMqEventPublisher(IConnection connection, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher, IAsyncDisposable
{
    public const string CorrelationIdHeader = "x-correlation-id";
    public const string SchemaVersionHeader = "x-schema-version";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _declaredExchanges = [];
    private IChannel? _channel;

    public async Task PublishAsync<TEvent>(TEvent @event, string exchange, string routingKey, CancellationToken ct = default)
        where TEvent : IntegrationEvent
    {
        var eventType = typeof(TEvent).Name;
        var messageId = @event.EventId.ToString();

        using var activity = TracePropagation.StartPublishActivity(exchange, routingKey, eventType, messageId);
        activity?.SetTag("correlation.id", @event.CorrelationId);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = EventBusJson.ContentType,
            ContentEncoding = "utf-8",
            MessageId = messageId,
            Type = eventType,
            CorrelationId = @event.CorrelationId,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                [CorrelationIdHeader] = @event.CorrelationId,
                [SchemaVersionHeader] = @event.SchemaVersion
            }
        };
        TracePropagation.Inject(properties);

        // Тип події, а не TEvent від базового класу: серіалізуються всі поля конкретної події
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), EventBusJson.Options);

        await _gate.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(exchange, ct);
            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, properties, body, ct);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, exception.Message);
            logger.LogError(exception, "Failed to publish {EventType} {EventId} to {Exchange}/{RoutingKey} (CorrelationId {CorrelationId})",
                eventType, @event.EventId, exchange, routingKey, @event.CorrelationId);
            throw;
        }
        finally
        {
            _gate.Release();
        }

        logger.LogInformation("Published {EventType} {EventId} to {Exchange}/{RoutingKey}, {SizeBytes} bytes (CorrelationId {CorrelationId})",
            eventType, @event.EventId, exchange, routingKey, body.Length, @event.CorrelationId);
    }

    private async Task<IChannel> GetChannelAsync(string exchange, CancellationToken ct)
    {
        if (_channel is not { IsOpen: true })
        {
            if (_channel is not null)
                await _channel.DisposeAsync();

            _channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                ct);
            _declaredExchanges.Clear();
        }

        // Publisher оголошує свій exchange сам: подія не загубиться через те, що споживачі ще не стартували
        // (exchange без прив'язаних черг повідомлення відкидає, але exchange має існувати)
        if (_declaredExchanges.Add(exchange))
            await Topology.DeclareExchangeAsync(_channel, exchange, ct);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        _gate.Dispose();
    }
}
