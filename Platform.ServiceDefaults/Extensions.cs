using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.HealthChecks;
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
    public const string LivenessEndpointPath = "/health/live";
    public const string ReadinessEndpointPath = "/health/ready";

    /// <summary>ActivitySource драйвера MongoDB (пакет MongoDB.Driver.Core.Extensions.DiagnosticSources).</summary>
    public const string MongoDbActivitySource = "MongoDB.Driver.Core.Extensions.DiagnosticSources";

    /// <summary>Meter і ActivitySource кешу кожного сервісу мають ім'я "{Сервіс}.Cache".</summary>
    public const string CacheMeterPattern = "*.Cache";

    /// <summary>
    /// Grpc.Net.Client створює власний span кожного виклику (grpc.method, grpc.status_code). Без підписки
    /// на це джерело span не експортується, і HTTP-span виклику «повисає» без батька у trace.
    /// </summary>
    public const string GrpcClientActivitySource = "Grpc.Net.Client";

    /// <summary>
    /// Polly (через Microsoft.Extensions.Http.Resilience) публікує метрики в Meter "Polly":
    /// resilience.polly.strategy.events (тег event.name: OnRetry, OnCircuitOpened, OnTimeout...),
    /// resilience.polly.strategy.attempt.duration, resilience.polly.pipeline.duration.
    /// </summary>
    public const string PollyMeterName = "Polly";

    /// <summary>
    /// Бізнес-метрики сервісів (Лаб. 07): Meter називається так само, як сервіс (ім'я збірки API),
    /// клас метрик лежить у &lt;Сервіс&gt;.Api/Metrics/.
    /// </summary>
    public static readonly string[] BusinessMeters = ["Enrollments.Api", "Courses.Api", "Reviews.WebApi"];

    /// <summary>Гістограми тривалості бізнес-операцій: "orders.operation.duration" тощо, у мс.</summary>
    public const string OperationDurationPattern = "*.operation.duration";

    /// <summary>Межі кошиків для тривалості операцій, мс: від швидких читань з кешу до повільних записів.</summary>
    private static readonly double[] OperationDurationBuckets = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000];

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
            // Standard Resilience Handler (Лаб. 07) для КОЖНОГО HttpClient і gRPC-клієнта — один раз, тут.
            // П'ять стратегій від зовнішньої до внутрішньої: rate limiter -> загальний таймаут -> retry
            // (експоненційна затримка з jitter) -> circuit breaker -> таймаут спроби. Спрацьовує на 5xx, 408,
            // 429, HttpRequestException і TimeoutRejectedException. Повтори POST/PUT/PATCH/DELETE вимкнено:
            // повторний запис міг би, наприклад, створити замовлення двічі.
            // Клієнт з іншими вимогами спершу знімає цей обробник (RemoveAllResilienceHandlers) і додає
            // власний — так обробники не нашаровуються (див. Platform.Aggregator/Resilience).
            http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

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
            // Вбудовані логи gRPC дублюють GrpcLoggingInterceptor; лишаємо лише попередження й помилки
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
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
                    .AddMeter(applicationName, sourcePrefix)   // власні метрики (наприклад, Gateway)
                    // Лаб. 06: метрики кешу кожного сервісу — Meter "Courses.Cache", "Reviews.Cache",
                    // "Platform.Aggregator.Cache": cache.hits / cache.misses з тегом cache.level (L1 | L2)
                    .AddMeter(CacheMeterPattern)
                    // Лаб. 07: бізнес-метрики сервісів і події resilience (OnRetry, OnCircuitOpened...)
                    .AddMeter(BusinessMeters)
                    .AddMeter(PollyMeterName)
                    // Кошики гістограм тривалості операцій у мілісекундах (за замовчуванням вони під секунди)
                    .AddView(OperationDurationPattern, new ExplicitBucketHistogramConfiguration { Boundaries = OperationDurationBuckets });
            })
            .WithTracing(tracing =>
            {
                tracing.SetSampler(CreateSampler(builder));

                // gRPC: клієнтський виклик — span Grpc.Net.Client, під ним HTTP-span (AddHttpClientInstrumentation);
                // серверний — span ASP.NET Core (AddAspNetCoreInstrumentation), GrpcLoggingInterceptor дописує rpc.*.
                // Заголовок traceparent у метаданих gRPC зв'язує spans клієнта й сервера в один trace.
                tracing.AddSource(applicationName, sourcePrefix)  // власні spans бізнес-операцій
                    .AddSource(CacheMeterPattern)                 // spans операцій кешу (Лаб. 06)
                    .AddSource(GrpcClientActivitySource)          // клієнтський span gRPC-виклику (батько HTTP POST)
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

    /// <summary>Тег перевірок liveness: лише сам процес, без зовнішніх залежностей.</summary>
    public const string LiveTag = "live";

    /// <summary>Тег перевірок readiness: бази, кеш, сервіси, без яких запит не обслужити (або обслужити гірше).</summary>
    public const string ReadyTag = "ready";

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Liveness: процес живий і відповідає — миттєво, без звернень назовні
            .AddCheck("self", () => HealthCheckResult.Healthy("process is running"), [LiveTag]);

        return builder;
    }

    /// <summary>
    /// Health endpoints (Лаб. 07). Мапляться в УСІХ середовищах, а не лише в Development: їх опитують
    /// Aspire (WithHttpHealthCheck) і Kubernetes (Лаб. 10). Назовні не видно рядків підключення —
    /// HealthResponseWriter показує лише назву, статус, тривалість і тип помилки (повний текст — у Development).
    /// <list type="bullet">
    /// <item>/health/live (і /alive) — liveness: лише "self". Збій -> оркестратор перезапускає процес.</item>
    /// <item>/health/ready — readiness: перевірки з тегом "ready" (бази, Redis, gRPC-залежності) з JSON-деталями.
    /// Збій -> сервіс тимчасово виключають з балансування; Degraded (Redis недоступний) лишається 200.</item>
    /// <item>/health — усі перевірки з деталями (діагностика).</item>
    /// </list>
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
        {
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        app.MapHealthChecks(LivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(LiveTag),
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(LiveTag)
        });

        app.MapHealthChecks(ReadinessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(ReadyTag),
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        return app;
    }

    /// <summary>
    /// CorrelationIdMiddleware — першим у конвеєрі, щоб ідентифікатор бачили всі наступні логи.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
