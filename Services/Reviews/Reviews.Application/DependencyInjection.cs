using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            // Порядок реєстрації = порядок виконання: Logging -> Validation -> Performance -> обробник
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        var thresholdMs = int.TryParse(configuration["Application:SlowRequestThresholdMs"], out var value) ? value : 500;
        services.AddSingleton(new PerformanceSettings(thresholdMs));

        return services;
    }
}
