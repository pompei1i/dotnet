using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Platform.ServiceDefaults;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Спільні налаштування платформи, які підключає кожен сервіс одним викликом AddServiceDefaults():
/// Serilog (структуровані логи), OpenTelemetry (трасування, метрики, експорт OTLP в Aspire Dashboard),
/// health checks, service discovery, resilience і наскрізний CorrelationId для HttpClient.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    /// <summary>ActivitySource драйвера MongoDB (пакет MongoDB.Driver.Core.Extensions.DiagnosticSources).</summary>
    public const string MongoDbActivitySource = "MongoDB.Driver.Core.Extensions.DiagnosticSources";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddSerilogLogging();

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        // CorrelationIdHandler читає ідентифікатор поточного вхідного запиту з HttpContext
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retry, timeout і circuit breaker для кожного HttpClient (детально — Лаб. 07)
            http.AddStandardResilienceHandler();

            // "https+http://orders-api" -> реальна адреса, яку Aspire передав змінними оточення
            http.AddServiceDiscovery();

            // Той самий X-Correlation-Id — у кожен вихідний запит до інших сервісів
            http.AddHttpMessageHandler<CorrelationIdHandler>();
        });

        return builder;
    }

    /// <summary>
    /// Serilog — основний провайдер логування. Кожен запис — об'єкт з окремими полями:
    /// у консоль пишеться CompactJson (вкладка Console logs), а через writeToProviders: true
    /// записи йдуть далі в провайдер OpenTelemetry і по OTLP — у вкладку Structured logs.
    /// </summary>
    public static TBuilder AddSerilogLogging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Ім'я ресурсу з AppHost (orders-api, gateway...) збігається з іменем сервісу в Dashboard;
        // без Aspire — ім'я збірки
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var isDevelopment = builder.Environment.IsDevelopment();

        // Стандартні провайдери (Console, Debug, EventLog) прибираємо, інакше з writeToProviders
        // кожен запис дублювався б у консолі. Провайдер OpenTelemetry додає ConfigureOpenTelemetry.
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog((services, configuration) => configuration
            .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            // Шумна інфраструктура: SQL і HTTP-виклики й так видно як spans у трасуванні
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.ServiceDiscovery", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Diagnostics.HealthChecks", LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .MinimumLevel.Override("MongoDB", LogEventLevel.Warning)
            .MinimumLevel.Override("Yarp", LogEventLevel.Warning)
            // Сервіс може точково перевизначити рівні в розділі "Serilog" свого appsettings.json
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()                       // CorrelationId з CorrelationIdMiddleware
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithEnvironmentName()                  // EnvironmentName: Development / Production
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new CompactJsonFormatter()),  // @t, @mt, @l, @tr (TraceId), @sp (SpanId) + властивості
            writeToProviders: true);

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var applicationName = builder.Environment.ApplicationName;

        // Власні ActivitySource і Meter шарів сервісу: "Orders.Api" -> також "Orders.*" (Orders.Bll тощо)
        var sourcePrefix = applicationName.Split('.')[0] + ".*";

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            // Атрибути ресурсу потрапляють у кожен span, лог і метрику сервісу
            .ConfigureResource(resource => resource.AddAttributes(
            [
                new("deployment.environment.name", builder.Environment.EnvironmentName),
                new("service.namespace", "online-courses-platform")
            ]))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()     // http.server.request.duration: RPS, тривалість, помилки
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(applicationName, sourcePrefix);  // власні метрики (наприклад, Gateway)
            })
            .WithTracing(tracing =>
            {
                tracing.SetSampler(CreateSampler(builder));

                tracing.AddSource(applicationName, sourcePrefix)  // власні spans бізнес-операцій
                    .AddSource(MongoDbActivitySource)             // команди MongoDB (сервіс відгуків)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Перевірки здоров'я не засмічують трасування
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        // Опитування health checks самим Aspire не цікаві
                        options.FilterHttpRequestMessage = request =>
                            request.RequestUri?.AbsolutePath.StartsWith(HealthEndpointPath) != true;
                    })
                    // Microsoft.Data.SqlClient: покриває і Dapper (Лаб. 02), і EF Core (Лаб. 03)
                    .AddSqlClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Development — 100 % трасувань. В інших середовищах — частка з OpenTelemetry:SamplingRatio
    /// (за замовчуванням 25 %); ParentBased зберігає рішення батьківського span, тож trace
    /// не «рветься» між сервісами.
    /// </summary>
    private static Sampler CreateSampler(IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
            return new AlwaysOnSampler();

        var ratio = builder.Configuration.GetValue("OpenTelemetry:SamplingRatio", 0.25);
        return new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio));
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Адресу колектора (Aspire Dashboard) Aspire передає змінною OTEL_EXPORTER_OTLP_ENDPOINT.
        // UseOtlpExporter експортує логи, метрики й трасування батчами у фоні — запити не чекають експорту.
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Liveness: процес живий і відповідає
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// /health — усі перевірки (зокрема бази, якщо сервіс їх зареєстрував); /alive — лише "live".
    /// Мапляться в усіх середовищах: їх опитують Aspire (WithHttpHealthCheck) і Kubernetes (Лаб. 10).
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath);

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }

    /// <summary>
    /// CorrelationIdMiddleware — першим у конвеєрі, щоб ідентифікатор бачили всі наступні логи.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
