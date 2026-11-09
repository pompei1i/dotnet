using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Hybrid;
using Reviews.Application.Caching;
using Reviews.Application.Common.Behaviors;

namespace Reviews.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Реєструє шар Application: MediatR зі скануванням обробників цієї збірки,
    /// pipeline behaviors і валідатори FluentValidation.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            // Ключ Community-ліцензії Lucky Penny (MediatR і AutoMapper) не зберігається в репозиторії:
            // змінна оточення LuckyPenny__LicenseKey або user-secrets
            cfg.LicenseKey = configuration["LuckyPenny:LicenseKey"];
            cfg.RegisterServicesFromAssembly(assembly);

            // Порядок реєстрації = порядок виконання: Tracing -> Logging -> Validation -> Caching -> Performance -> обробник.
            // Tracing — найзовнішній, щоб логи всіх інших behaviors мали SpanId бізнес-операції.
            // Caching — після валідації (некоректний запит не кешується) і перед Performance: попадання
            // в кеш не доходить до обробника, тож PerformanceBehavior міряє лише справжні звернення до бази
            cfg.AddOpenBehavior(typeof(TracingBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        // Кеш (Лаб. 06): HybridCache — готовий дворівневий кеш .NET. L1 — IMemoryCache процесу,
        // L2 — зареєстрований IDistributedCache (Redis, AddRedisDistributedCache у WebApi); без нього
        // працює лише L1. Ключі "reviews:*", серіалізація — System.Text.Json. Розмір одного значення
        // обмежено, щоб випадково великий результат не витіснив решту записів L1.
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 512 * 1024;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromSeconds(30),
                Expiration = TimeSpan.FromMinutes(5)
            };
        });
        services.AddMetrics();   // IMeterFactory для ReviewsCacheMetrics (у застосунку його вже додає хост)
        services.AddSingleton<ReviewsCacheMetrics>();
        services.AddScoped<IReviewsCacheInvalidator, ReviewsCacheInvalidator>();

        services.AddValidatorsFromAssembly(assembly);

        var thresholdMs = int.TryParse(configuration["Application:SlowRequestThresholdMs"], out var value) ? value : 500;
        services.AddSingleton(new PerformanceSettings(thresholdMs));

        return services;
    }
}
