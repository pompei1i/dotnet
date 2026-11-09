using StackExchange.Redis;
using Reviews.Application;
using Reviews.Infrastructure;
using Platform.ServiceDefaults.Grpc;
using Reviews.WebApi;
using Reviews.WebApi.Grpc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи (Лаб. 05): Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Aspire передає рядок підключення до бази як ConnectionStrings:FeedbackDb, а Infrastructure
// читає розділ MongoDb. Без Aspire лишається резервний MongoDb:ConnectionString з appsettings.json.
if (builder.Configuration.GetConnectionString("FeedbackDb") is { Length: > 0 } feedbackDb)
    builder.Configuration["MongoDb:ConnectionString"] = feedbackDb;

// ---------- Кешування (Лаб. 06) ----------
// L1 HybridCache — цей IMemoryCache. SizeLimit у байтах: HybridCache задає кожному запису Size, рівний
// розміру його серіалізованого значення, тож L1 не займе більше 50 МБ; TrackStatistics — для діагностики.
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 50L * 1024 * 1024;
    options.CompactionPercentage = 0.2;
    options.TrackStatistics = true;
});

// L2 — IDistributedCache поверх Redis через Aspire-інтеграцію (ConnectionStrings:redis з AppHost).
// HybridCache з Application-шару підхоплює його автоматично. Без Aspire (рядка підключення немає,
// наприклад dotnet run або тести) сервіс працює лише з L1
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

// ---------- gRPC (Лаб. 06) ----------
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<GrpcLoggingInterceptor>();
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Кожен шар реєструє себе сам; WebApi знає про Infrastructure лише тут, під час старту
builder.Services
    .AddApplication(builder.Configuration)       // MediatR, behaviors, валідатори
    .AddInfrastructure(builder.Configuration)    // MongoDB: мапінг, репозиторії, індекси, сідінг, health check
    .AddWebApi(builder.Configuration);           // контролери, AutoMapper, ProblemDetails, Swagger

var app = builder.Build();

// CorrelationId з ServiceDefaults стоїть першим, щоб його бачили всі логи запиту, зокрема логи behaviors
app.UseCorrelationId();

// Підсумковий запис про запит — зовні обробника винятків: він бачить уже остаточний статус
// (400, 404, 409...) і пише його на рівні Information, а не як необроблену помилку
app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostic, http) =>
{
    diagnostic.Set("CorrelationId", http.Items[Platform.ServiceDefaults.CorrelationIdMiddleware.HeaderName]);
    diagnostic.Set("UserId", http.User.Identity?.Name ?? "anonymous");
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();   // gRPC-API відгуків для Aggregator (endpoint "grpc", HTTP/2)
app.MapDefaultEndpoints();   // /health (зокрема перевірка MongoDB) і /alive

app.Run();

// Потрібно для WebApplicationFactory<Program> в API-тестах
public partial class Program;
