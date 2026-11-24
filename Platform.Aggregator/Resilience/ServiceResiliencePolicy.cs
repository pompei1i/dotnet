namespace Platform.Aggregator.Resilience;

/// <summary>
/// Параметри стійкості одного клієнта Aggregator (Лаб. 07). Чому саме такі значення — README,
/// розділ «Політики resilience клієнтів Aggregator».
///
/// Шари і як вони узгоджені (для одного gRPC-виклику):
/// <code>
/// deadline виклику (CallDeadline)                         — верхня межа всього, передається й серверу
///  └─ gRPC RetryPolicy (GrpcMaxAttempts, backoff)          — повтори за gRPC-статусами (UNAVAILABLE...)
///      └─ Standard Resilience Handler (HttpClient)         — один HTTP/2-запит
///          ├─ TotalRequestTimeout                          — межа для всіх HTTP-спроб
///          ├─ Retry (HttpRetries, Delay, jitter)            — мережеві збої й таймаути спроби
///          ├─ Circuit breaker (FailureRatio, MinimumThroughput, SamplingDuration, BreakDuration)
///          └─ AttemptTimeout                               — одна спроба
/// </code>
/// Правило узгодження: TotalRequestTimeout ≥ (HttpRetries + 1) × AttemptTimeout + затримки,
/// CallDeadline ≥ TotalRequestTimeout, SamplingDuration ≥ 2 × AttemptTimeout (інакше валідація
/// опцій не пропустить застосунок). Перевіряє це Validate() під час старту.
/// </summary>
public sealed record ServiceResiliencePolicy
{
    /// <summary>Логічне ім'я ресурсу в AppHost — воно ж у логах і тегах метрик.</summary>
    public required string Service { get; init; }

    /// <summary>Критичний — без його даних відповідь неможлива або втрачає сенс.</summary>
    public required bool Critical { get; init; }

    // ---- HTTP (Standard Resilience Handler) ----
    public required int HttpRetries { get; init; }
    public required TimeSpan RetryDelay { get; init; }
    public required TimeSpan AttemptTimeout { get; init; }
    public required TimeSpan TotalRequestTimeout { get; init; }
    public required double BreakerFailureRatio { get; init; }
    public required int BreakerMinimumThroughput { get; init; }
    public required TimeSpan BreakerSamplingDuration { get; init; }
    public required TimeSpan BreakDuration { get; init; }

    // ---- gRPC RetryPolicy (статуси в трейлерах HTTP 200 HTTP-обробник не бачить) ----
    public required int GrpcMaxAttempts { get; init; }
    public required TimeSpan GrpcInitialBackoff { get; init; }
    public required TimeSpan GrpcMaxBackoff { get; init; }

    /// <summary>Deadline одного gRPC-виклику разом з усіма повторами.</summary>
    public required TimeSpan CallDeadline { get; init; }

    public ServiceResiliencePolicy Validate()
    {
        var httpBudget = AttemptTimeout * (HttpRetries + 1) + RetryDelay * HttpRetries * 2;
        if (TotalRequestTimeout < httpBudget)
            throw new InvalidOperationException($"{Service}: TotalRequestTimeout {TotalRequestTimeout} < retries budget {httpBudget}");
        if (CallDeadline < TotalRequestTimeout)
            throw new InvalidOperationException($"{Service}: CallDeadline {CallDeadline} < TotalRequestTimeout {TotalRequestTimeout}");
        if (BreakerSamplingDuration < AttemptTimeout * 2)
            throw new InvalidOperationException($"{Service}: SamplingDuration must be at least twice the AttemptTimeout");
        return this;
    }

    /// <summary>
    /// enrollments-api — КРИТИЧНИЙ і обов'язковий: без замовлення картки немає (503).
    /// Читання ідемпотентні, тож повторів більше; breaker чутливий — краще швидко відмовити, ніж
    /// тримати запити клієнтів, поки замовлення не читаються.
    /// </summary>
    public static readonly ServiceResiliencePolicy Enrollments = new ServiceResiliencePolicy
    {
        Service = "enrollments-api",
        Critical = true,
        HttpRetries = 2,
        RetryDelay = TimeSpan.FromMilliseconds(100),
        AttemptTimeout = TimeSpan.FromSeconds(1.5),
        TotalRequestTimeout = TimeSpan.FromSeconds(5),
        BreakerFailureRatio = 0.3,
        BreakerMinimumThroughput = 5,
        BreakerSamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(10),
        GrpcMaxAttempts = 3,
        GrpcInitialBackoff = TimeSpan.FromMilliseconds(200),
        GrpcMaxBackoff = TimeSpan.FromSeconds(1),
        CallDeadline = TimeSpan.FromSeconds(6)
    }.Validate();

    /// <summary>
    /// courses-api — КРИТИЧНИЙ (Каталог): без даних курсів картка втрачає головне — актуальну ціну
    /// й опис. Ті самі значення, що й для замовлень: більше повторів, чутливий breaker (30 %).
    /// </summary>
    public static readonly ServiceResiliencePolicy Courses = Enrollments with { Service = "courses-api" };

    /// <summary>
    /// reviews-api — НЕКРИТИЧНИЙ: Aggregator і без відгуків поверне часткову відповідь (Лаб. 05).
    /// Швидко здатися: один повтор, коротка спроба (1 с) і загальний бюджет 2.5 с, щоб картка не чекала
    /// відгуків. Breaker терпиміший (70 % збоїв), але розімкнений довше (20 с): відгуки можна не
    /// показувати довше, ніж замовлення, — сервіс має час відновитися.
    /// </summary>
    public static readonly ServiceResiliencePolicy Reviews = new ServiceResiliencePolicy
    {
        Service = "reviews-api",
        Critical = false,
        HttpRetries = 1,
        RetryDelay = TimeSpan.FromMilliseconds(100),
        AttemptTimeout = TimeSpan.FromSeconds(1),
        TotalRequestTimeout = TimeSpan.FromSeconds(2.5),
        BreakerFailureRatio = 0.7,
        BreakerMinimumThroughput = 5,
        BreakerSamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(20),
        GrpcMaxAttempts = 2,
        GrpcInitialBackoff = TimeSpan.FromMilliseconds(100),
        GrpcMaxBackoff = TimeSpan.FromMilliseconds(500),
        CallDeadline = TimeSpan.FromSeconds(3)
    }.Validate();
}
