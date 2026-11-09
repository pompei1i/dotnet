using StackExchange.Redis;
using System.Text.Json.Serialization;
using Courses.Grpc;
using Enrollments.Grpc;
using Microsoft.Extensions.Http.Resilience;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
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
builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new("http://_grpc.enrollments-api"));
builder.Services.AddGrpcClient<CoursesService.CoursesServiceClient>(o => o.Address = new("http://_grpc.courses-api"));
builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new("http://_grpc.reviews-api"));

// Обгортки над згенерованими клієнтами: логування, deadline, NOT_FOUND -> null
builder.Services.AddScoped<EnrollmentsClient>();
builder.Services.AddScoped<CoursesClient>();
builder.Services.AddScoped<ReviewsClient>();

// ---------- Кеш агрегованих відповідей (Лаб. 06) ----------
// IDistributedCache поверх Redis через Aspire-інтеграцію; ConnectionStrings:redis передає AppHost.
// Без Aspire — кеш у пам'яті процесу, щоб агрегатор запускався й окремо
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis", configureOptions: redis =>
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

// Стандартний resilience-обробник (ServiceDefaults) за замовчуванням чекає до 30 с і робить 3 повтори
// з паузами в секунди. Агрегатор має віддати часткову відповідь швидко: зупинений в Aspire сервіс
// не відмовляє одразу (з'єднання приймає проксі Aspire), тож рятує саме короткий timeout спроби.
// Докладно політики стійкості — Лаб. 07
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(options =>
{
    options.Retry.MaxRetryAttempts = 1;
    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1.5);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
});

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
