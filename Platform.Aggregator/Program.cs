using StackExchange.Redis;
using System.Text.Json.Serialization;
using Courses.Grpc;
using Enrollments.Grpc;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Resilience;
using Reviews.Grpc;
using Platform.Aggregator.ErrorHandling;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, resilience і CorrelationIdHandler
// для всіх HttpClient — з ServiceDefaults
builder.AddServiceDefaults();

// ---------- gRPC-клієнти мікросервісів (Лаб. 06, замість typed HttpClient Лаб. 05) ----------
// Типізовані клієнти згенеровано з .proto сервісів. Адреса — логічне ім'я ресурсу з AppHost:
// "_grpc." вибирає endpoint "grpc" (HTTP/2 без TLS), реальний host:port підставляє service discovery.
// Фабрика gRPC-клієнтів будує їх на IHttpClientFactory, тож ConfigureHttpClientDefaults з ServiceDefaults
// діє і тут: service discovery, resilience і CorrelationIdHandler (X-Correlation-Id у метаданих виклику).
// Канал HTTP/2 один на сервіс і живе довго: паралельні виклики мультиплексуються в одному з'єднанні.
//
// Лаб. 07: кожен клієнт має власну політику стійкості (Resilience/ServiceResiliencePolicy.cs):
// стандартний обробник ServiceDefaults знімається й замінюється налаштованим під критичність сервісу,
// а повтори за gRPC-статусами (UNAVAILABLE) робить RetryPolicy каналу.
builder.Services.AddResilientGrpcClient<OrdersService.OrdersServiceClient>("http://_grpc.enrollments-api", ServiceResiliencePolicy.Enrollments);
builder.Services.AddResilientGrpcClient<CoursesService.CoursesServiceClient>("http://_grpc.courses-api", ServiceResiliencePolicy.Courses);
builder.Services.AddResilientGrpcClient<ReviewsService.ReviewsServiceClient>("http://_grpc.reviews-api", ServiceResiliencePolicy.Reviews);

// Обгортки над згенерованими клієнтами: логування, deadline, NOT_FOUND -> null
builder.Services.AddScoped<EnrollmentsClient>();
builder.Services.AddScoped<CoursesClient>();
builder.Services.AddScoped<ReviewsClient>();

// ---------- Кеш агрегованих відповідей (Лаб. 06) ----------
// IDistributedCache поверх Redis через Aspire-інтеграцію; ConnectionStrings:redis передає AppHost.
// Без Aspire — кеш у пам'яті процесу, щоб агрегатор запускався й окремо
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
    // Власний health check "redis-cache" зі статусом Degraded (HealthChecks/) замість вбудованого
    // Unhealthy-перевірки Aspire-інтеграції: недоступний кеш не робить агрегатор неготовим
    builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true, redis =>
    {
        // Redis — прискорювач: якщо він недоступний, команди мають падати одразу (FailFast), а не стояти
        // в черзі до timeout (5 с за замовчуванням), інакше кожен запит чекав би секунди замість мілісекунд
        redis.BacklogPolicy = BacklogPolicy.FailFast;
        redis.ConnectTimeout = 2000;
        redis.AsyncTimeout = 1000;
        redis.SyncTimeout = 1000;
        redis.AbortOnConnectFail = false;
    });
else
    builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<AggregateCacheMetrics>();
builder.Services.AddScoped<AggregateCache>();

// ---------- Health checks залежностей (Лаб. 07) ----------
// gRPC Health Checking Protocol кожного сервісу + Redis (Degraded) — HealthChecks/AggregatorHealthChecks.cs
builder.AddAggregatorHealthChecks();

builder.Services.AddScoped<OrderCardService>();
builder.Services.AddScoped<CourseOverviewService>();

// JSON відповіді: camelCase (за замовчуванням), null-поля — дані недоступного сервісу — не серіалізуються
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString()
                                                   ?? context.HttpContext.TraceIdentifier;
    if (context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] is string correlationId)
        context.ProblemDetails.Extensions["correlationId"] = correlationId;
});
builder.Services.AddExceptionHandler<UpstreamUnavailableHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// CorrelationId — першим: його бачать усі логи, а CorrelationIdHandler передає далі в сервіси
app.UseCorrelationId();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
