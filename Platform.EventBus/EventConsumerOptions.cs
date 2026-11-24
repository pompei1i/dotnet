namespace Platform.EventBus;

/// <summary>Налаштування одного споживача: черга, exchange, bindings і обробники за типами подій.</summary>
public sealed class EventConsumerOptions
{
    public required string Queue { get; init; }

    public required string Exchange { get; init; }

    /// <summary>Шаблони routing key для bindings черги ("course.*", "course.renamed").</summary>
    public List<string> BindingKeys { get; } = [];

    /// <summary>Ім'я типу події (BasicProperties.Type) -> тип події і тип обробника.</summary>
    public Dictionary<string, (Type Event, Type Handler)> Handlers { get; } = new(StringComparer.Ordinal);

    /// <summary>Скільки непідтверджених повідомлень споживач може тримати одночасно (BasicQos).</summary>
    public ushort Prefetch { get; set; } = 10;

    /// <summary>Скільки разів повторювати тимчасовий збій, перш ніж відправити повідомлення в DLQ.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Пауза перед повтором: спроба N чекає RetryDelay × 2^(N-1).</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>Fluent-реєстрація обробників: AddEventConsumer(...).Bind("course.*").Handle&lt;Event, Handler&gt;().</summary>
public sealed class EventConsumerBuilder(EventConsumerOptions options, Microsoft.Extensions.DependencyInjection.IServiceCollection services)
{
    public EventConsumerOptions Options => options;

    public EventConsumerBuilder Bind(string routingKeyPattern)
    {
        options.BindingKeys.Add(routingKeyPattern);
        return this;
    }

    public EventConsumerBuilder Handle<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        options.Handlers[typeof(TEvent).Name] = (typeof(TEvent), typeof(THandler));
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<THandler>(services);
        return this;
    }
}
