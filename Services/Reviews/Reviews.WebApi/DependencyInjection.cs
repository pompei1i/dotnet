using Reviews.WebApi.ErrorHandling;
using Reviews.WebApi.Middleware;

namespace Reviews.WebApi;

public static class DependencyInjection
{
    /// <summary>
    /// Реєструє шар Presentation: контролери, профілі AutoMapper (DTO і HTTP-контракти),
    /// ProblemDetails із глобальним обробником винятків і Swagger.
    /// </summary>
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        // Валідація — лише FluentValidation у pipeline MediatR: неявне [Required] для non-nullable
        // рядків вимкнено, тож порожні поля доходять до валідатора команди й усі помилки
        // повертаються разом в одному форматі
        services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

        // Профілі з Application (сутність -> DTO) і WebApi (HTTP-контракт -> команда).
        // Один ключ Lucky Penny покриває і AutoMapper, і MediatR
        services.AddAutoMapper(cfg =>
        {
            cfg.LicenseKey = configuration["LuckyPenny:LicenseKey"];
            cfg.AddMaps(typeof(Application.DependencyInjection).Assembly, typeof(DependencyInjection).Assembly);
        });

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            if (context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] is string correlationId)
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
