using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.EventBus;

/// <summary>Реєстрація шини подій у Program.cs сервісу.</summary>
public static class EventBusExtensions
{
    /// <summary>Ім'я ресурсу RabbitMQ в AppHost; рядок підключення — ConnectionStrings:messaging.</summary>
    public const string ConnectionName = "messaging";

    public static bool IsEventBusConfigured(this IHostApplicationBuilder builder)
        => !string.IsNullOrEmpty(builder.Configuration.GetConnectionString(ConnectionName));

    /// <summary>
    /// Підключення через Aspire.RabbitMQ.Client: AddRabbitMQClient("messaging") реєструє IConnection
    /// (один на застосунок, автоматичне відновлення з'єднання) і health check брокера. Вбудоване трасування
    /// вимкнено: контекст через брокер передає TracePropagation. Без рядка підключення (тести, запуск без
    /// Aspire) події не публікуються — NoOpEventPublisher лише логує це.
    /// </summary>
    public static IHostApplicationBuilder AddEventBus(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMetrics();
        builder.Services.TryAddSingleton<MessagingMetrics>();

        if (builder.IsEventBusConfigured())
        {
            builder.AddRabbitMQClient(ConnectionName, settings => settings.DisableTracing = true);
            builder.Services.TryAddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        }
        else
        {
            builder.Services.TryAddSingleton<IEventPublisher, NoOpEventPublisher>();
        }

        return builder;
    }

    /// <summary>Споживач однієї черги; обробники додаються через Handle&lt;TEvent, THandler&gt;().</summary>
    public static EventConsumerBuilder AddEventConsumer(this IHostApplicationBuilder builder, string queue, string exchange)
    {
        var options = new EventConsumerOptions { Queue = queue, Exchange = exchange };

        // Кілька споживачів у сервісі — кілька екземплярів одного типу, тому не AddHostedService<T>()
        if (builder.IsEventBusConfigured())
            builder.Services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<RabbitMqEventConsumer>(sp, options));

        return new EventConsumerBuilder(options, builder.Services);
    }
}

/// <summary>Publisher без брокера (тести, запуск сервісу окремо): подія не відправляється, факт логується.</summary>
public sealed class NoOpEventPublisher(ILogger<NoOpEventPublisher> logger) : IEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent @event, string exchange, string routingKey, CancellationToken ct = default)
        where TEvent : IntegrationEvent
    {
        logger.LogWarning("Message broker is not configured: {EventType} {EventId} for {Exchange}/{RoutingKey} was not published",
            typeof(TEvent).Name, @event.EventId, exchange, routingKey);
        return Task.CompletedTask;
    }
}
