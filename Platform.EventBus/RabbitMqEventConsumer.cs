using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>
/// Споживач подій як BackgroundService (Лаб. 08): один канал, декларативна топологія, BasicQos,
/// AsyncEventingBasicConsumer і BasicConsumeAsync з autoAck: false — повідомлення видаляється з черги
/// лише після явного підтвердження.
///
/// Рішення щодо кожного повідомлення:
/// <list type="bullet">
/// <item>успіх — BasicAckAsync;</item>
/// <item>постійний збій (некоректний JSON, порожній EventId, PermanentEventException) — BasicNackAsync
/// з requeue: false: RabbitMQ переносить оригінал у DLX → DLQ і додає заголовок x-death;</item>
/// <item>тимчасовий збій (база недоступна, таймаут) — повтор: оригінал підтверджується, а копія
/// публікується знову з x-retry-count + 1 (requeue не підходить — заголовки повернутого повідомлення не
/// змінити, тож спроби не порахувати). Після MaxRetries спроб — Nack без requeue → DLQ;</item>
/// <item>тип події без обробника (binding за шаблоном приніс подію, яка сервісу не потрібна) — ack з логом.</item>
/// </list>
/// Повтор публікується через default exchange прямо в СВОЮ чергу, а не в topic exchange: інакше копію
/// отримали б і черги інших сервісів. Під час зупинки застосунку непідтверджене повідомлення не
/// підтверджується — після закриття каналу RabbitMQ поверне його в чергу.
/// </summary>
public sealed class RabbitMqEventConsumer(
    EventConsumerOptions options,
    IServiceProvider services,
    MessagingMetrics metrics,
    ILogger<RabbitMqEventConsumer> logger) : BackgroundService
{
    public const string RetryCountHeader = "x-retry-count";

    private static readonly TimeSpan DepthInterval = TimeSpan.FromSeconds(10);

    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Брокер може бути ще недоступний — пробуємо підключитися, доки застосунок працює
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                break;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Consumer {Queue} could not start: {FailureType} {FailureMessage}; retrying in 5 s",
                    options.Queue, exception.GetType().Name, exception.GetBaseException().Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        // Поки працюємо — раз на 10 с оновлюємо gauge глибини черги і DLQ
        using var timer = new PeriodicTimer(DepthInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RecordQueueDepthAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var connection = services.GetRequiredService<IConnection>();
        _channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await Topology.DeclareQueueAsync(_channel, options.Exchange, options.Queue, options.BindingKeys, ct);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: options.Prefetch, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => OnMessageAsync(delivery, ct);
        await _channel.BasicConsumeAsync(options.Queue, autoAck: false, consumer, ct);

        logger.LogInformation("Consumer {Queue} started: exchange {Exchange}, bindings {BindingKeys}, events {EventTypes}, prefetch {Prefetch}",
            options.Queue, options.Exchange, options.BindingKeys, options.Handlers.Keys, options.Prefetch);
    }

    private async Task OnMessageAsync(BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var channel = _channel!;
        var properties = delivery.BasicProperties;
        var eventType = properties.Type ?? "unknown";
        var messageId = properties.MessageId ?? "none";
        var correlationId = HeaderText(properties.Headers, RabbitMqEventPublisher.CorrelationIdHeader) ?? properties.CorrelationId;
        var attempt = RetryCount(properties.Headers);

        using var activity = TracePropagation.StartConsumerActivity(delivery, options.Queue);
        activity?.SetTag("correlation.id", correlationId);
        activity?.SetTag("messaging.retry.count", attempt);

        // Структуровані властивості в усіх логах обробки цього повідомлення (Serilog бере їх зі scope)
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["EventId"] = messageId,
            ["EventType"] = eventType,
            ["Queue"] = options.Queue,
            ["RetryCount"] = attempt
        });

        var started = Stopwatch.GetTimestamp();
        string outcome;
        try
        {
            if (!options.Handlers.TryGetValue(eventType, out var registration))
            {
                logger.LogWarning("No handler for {EventType} {EventId} in {Queue}; acknowledged and ignored", eventType, messageId, options.Queue);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
                outcome = "ignored";
            }
            else
            {
                var @event = Deserialize(delivery, registration.Event);

                await using (var messageScope = services.CreateAsyncScope())
                {
                    var handler = (IIntegrationEventHandler)messageScope.ServiceProvider.GetRequiredService(registration.Handler);
                    await handler.HandleAsync(@event, stoppingToken);
                }

                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
                outcome = "ack";
                logger.LogInformation("Handled {EventType} {EventId} from {Queue} in {ElapsedMs} ms (attempt {Attempt})",
                    eventType, messageId, options.Queue, ElapsedMs(started), attempt + 1);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Застосунок зупиняється: без ack повідомлення повернеться в чергу після закриття каналу
            logger.LogInformation("Consumer {Queue} stopping; {EventType} {EventId} left unacknowledged for redelivery",
                options.Queue, eventType, messageId);
            return;
        }
        catch (Exception exception) when (exception is JsonException or PermanentEventException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            logger.LogError(exception, "{EventType} {EventId} cannot be processed ({FailureType}: {FailureMessage}); sending to DLQ {DeadLetterQueue}",
                eventType, messageId, exception.GetType().Name, exception.Message, Topology.DeadLetterQueue(options.Queue));
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            outcome = "dead_letter";
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            outcome = await RetryOrDeadLetterAsync(channel, delivery, attempt, exception, stoppingToken);
        }

        metrics.Processed(options.Queue, eventType, outcome, ElapsedMs(started));
    }

    private async Task<string> RetryOrDeadLetterAsync(
        IChannel channel, BasicDeliverEventArgs delivery, int attempt, Exception exception, CancellationToken stoppingToken)
    {
        var properties = delivery.BasicProperties;

        if (attempt >= options.MaxRetries)
        {
            logger.LogError(exception, "{EventType} {EventId} failed after {Attempts} attempts ({FailureType}: {FailureMessage}); sending to DLQ {DeadLetterQueue}",
                properties.Type, properties.MessageId, attempt + 1, exception.GetType().Name, exception.GetBaseException().Message,
                Topology.DeadLetterQueue(options.Queue));
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return "dead_letter";
        }

        var delay = options.RetryDelay * Math.Pow(2, attempt);
        logger.LogWarning(exception, "{EventType} {EventId} failed ({FailureType}: {FailureMessage}); retry {Retry}/{MaxRetries} in {RetryDelay}",
            properties.Type, properties.MessageId, exception.GetType().Name, exception.GetBaseException().Message,
            attempt + 1, options.MaxRetries, delay);

        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return "retry";   // зупинка під час паузи: без ack повідомлення повернеться в чергу
        }

        var retry = new BasicProperties(properties)
        {
            Headers = new Dictionary<string, object?>(properties.Headers ?? new Dictionary<string, object?>())
            {
                [RetryCountHeader] = attempt + 1
            }
        };

        // Спершу нова копія, потім ack оригіналу: якщо процес впаде між ними, буде дублікат,
        // а не втрата — дублікат відсіє ідемпотентний обробник
        await channel.BasicPublishAsync(exchange: "", routingKey: options.Queue, mandatory: false, retry, delivery.Body, CancellationToken.None);
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
        return "retry";
    }

    private static IntegrationEvent Deserialize(BasicDeliverEventArgs delivery, Type eventType)
    {
        var @event = (IntegrationEvent?)JsonSerializer.Deserialize(delivery.Body.Span, eventType, EventBusJson.Options)
                     ?? throw new JsonException("Empty message body");

        if (@event.EventId == Guid.Empty)
            throw new PermanentEventException("EventId is missing");

        return @event;
    }

    private async Task RecordQueueDepthAsync(CancellationToken ct)
    {
        if (_channel is not { IsOpen: true } channel)
            return;
        try
        {
            metrics.QueueDepth(options.Queue, await channel.MessageCountAsync(options.Queue, ct));
            metrics.QueueDepth(Topology.DeadLetterQueue(options.Queue), await channel.MessageCountAsync(Topology.DeadLetterQueue(options.Queue), ct));
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug("Queue depth of {Queue} unavailable: {FailureMessage}", options.Queue, exception.Message);
        }
    }

    private static int RetryCount(IDictionary<string, object?>? headers)
        => headers is not null && headers.TryGetValue(RetryCountHeader, out var value) && value is not null
            ? Convert.ToInt32(value)
            : 0;

    private static string? HeaderText(IDictionary<string, object?>? headers, string key)
        => headers is not null && headers.TryGetValue(key, out var value)
            ? value is byte[] bytes ? System.Text.Encoding.UTF8.GetString(bytes) : value?.ToString()
            : null;

    private static double ElapsedMs(long started) => Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }
    }
}
