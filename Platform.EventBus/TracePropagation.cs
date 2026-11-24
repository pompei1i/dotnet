using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>
/// Передача контексту трасування через брокер (Лаб. 08). Без неї обробка події в іншому сервісі була б
/// окремим, ні з чим не пов'язаним trace.
/// <list type="bullet">
/// <item>Публікація: span типу Producer, його контекст записується в заголовки повідомлення у форматі
/// W3C traceparent (tracestate, baggage) пропагатором OpenTelemetry.</item>
/// <item>Споживання: контекст витягується з заголовків (значення приходять як byte[]), і span типу Consumer
/// стає його нащадком: HTTP-запит → publish → process в іншому сервісі → запити до БД — один trace.</item>
/// </list>
/// ActivitySource "Platform.Messaging" ServiceDefaults додає в OpenTelemetry (AddSource). Вбудоване
/// трасування Aspire-інтеграції RabbitMQ вимкнено, щоб заголовки traceparent не записувалися двічі.
/// </summary>
public static class TracePropagation
{
    public const string SourceName = "Platform.Messaging";

    public static readonly ActivitySource Source = new(SourceName);

    private static TextMapPropagator Propagator => Propagators.DefaultTextMapPropagator;

    public static Activity? StartPublishActivity(string exchange, string routingKey, string eventType, string messageId)
    {
        var activity = Source.StartActivity($"{exchange} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.type", "send");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("messaging.message.type", eventType);
        return activity;
    }

    /// <summary>Записує контекст поточного Activity (span публікації) у заголовки повідомлення.</summary>
    public static void Inject(BasicProperties properties)
    {
        if (Activity.Current is not { } activity)
            return;

        properties.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), properties.Headers,
            static (headers, key, value) => headers[key] = value);
    }

    public static Activity? StartConsumerActivity(BasicDeliverEventArgs delivery, string queue)
    {
        var parent = Propagator.Extract(default, delivery.BasicProperties.Headers, static (headers, key) =>
            headers is not null && headers.TryGetValue(key, out var value)
                ? [value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString() ?? ""]
                : []);
        Baggage.Current = parent.Baggage;

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.type", "process");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", delivery.RoutingKey);
        activity?.SetTag("messaging.message.id", delivery.BasicProperties.MessageId);
        activity?.SetTag("messaging.message.type", delivery.BasicProperties.Type);
        return activity;
    }
}
