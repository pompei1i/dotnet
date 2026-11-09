using StackExchange.Redis;
using Courses.Api.Controllers;
using Courses.Api.Grpc;
using Courses.Bll.Caching;
using Courses.Bll.Mapping;
using Courses.Bll.Services;
using Courses.Bll.Validation;
using Courses.Dal;
using Courses.Dal.Seeding;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Grpc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи (Лаб. 05): Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Рядок підключення лежить в appsettings.json і перевизначається змінною оточення
// ConnectionStrings__CoursesDb — саме так його передає .NET Aspire (Platform.AppHost).
var connectionString = builder.Configuration.GetConnectionString("CoursesDb")
    ?? throw new InvalidOperationException("Connection string 'CoursesDb' is missing");

// DAL: DbContext і Unit of Work — Scoped, один контекст на HTTP-запит
builder.Services.AddDbContext<CoursesDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ---------- Кешування (Лаб. 06) ----------

// L1 — IMemoryCache у пам'яті процесу. SizeLimit обмежує його: кожен запис має Size у KiB свого JSON,
// тож разом не більше ~10 МБ; при переповненні витісняється 20 % записів — спершу Low-пріоритет
// (сторінки списку), потім Normal (картки), High (довідники) — в останню чергу. TrackStatistics —
// для метрик cache.l1.* і health check.
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10_000;
    options.CompactionPercentage = 0.2;
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(1);
    options.TrackStatistics = true;
});

// L2 — IDistributedCache поверх Redis через Aspire-інтеграцію. Рядок підключення ConnectionStrings:redis
// передає AppHost; інтеграція сама додає повторні з'єднання, health check і трасування команд Redis.
// Без Aspire (dotnet run, тести) рядка немає — тоді L2 замінює MemoryDistributedCache і кеш фактично однорівневий
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

builder.Services.Configure<CachingOptions>(builder.Configuration.GetSection(CachingOptions.SectionName));
builder.Services.AddSingleton<CacheMetrics>();
builder.Services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
builder.Services.AddHostedService<CacheWarmupService>();
builder.Services.AddHealthChecks().AddCheck<CacheHealthCheck>("cache");

// ---------- gRPC (Лаб. 06) ----------

// Серверний interceptor з ServiceDefaults логує кожен виклик (метод, peer, статус, тривалість).
// Endpoint "grpc" (HTTP/2) описано в Kestrel:Endpoints у appsettings.json
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<GrpcLoggingInterceptor>();
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// BLL: сервіси
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IInstructorService, InstructorService>();

// BLL: профілі AutoMapper реєструються централізовано. Ключ Community-ліцензії не зберігається
// в репозиторії — його задають змінною оточення AutoMapper__LicenseKey або через user-secrets.
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<CourseProfile>();
    cfg.AddProfile<CategoryProfile>();
    cfg.AddProfile<InstructorProfile>();
});

// FluentValidation: валідатори з BLL реєструються в DI і викликаються в контролерах явно
// (застаріле автоматичне AddFluentValidation не використовується)
builder.Services.AddValidatorsFromAssemblyContaining<CreateCourseDtoValidator>();

builder.Services.AddControllers();

// Помилки — у форматі ProblemDetails; винятки перетворює один глобальний обробник
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    if (context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] is string correlationId)
        context.ProblemDetails.Extensions["correlationId"] = correlationId;
});
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Міграції та сідінг під час старту. Обидва кроки вимикаються конфігурацією
// (Database:MigrateOnStartup, Database:SeedOnStartup), тож сумісні з CI/CD;
// у Production сідінг вимкнено за замовчуванням.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CoursesDbContext>();

    await db.Database.MigrateAsync();   // схема з міграцій + довідник категорій з HasData

    if (app.Configuration.GetValue("Database:SeedOnStartup", !app.Environment.IsProduction()))
    {
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));
        await DbInitializer.SeedAsync(db, seedLogger);   // решта даних — програмно, з перевіркою наявності
    }
}

// CorrelationId з ServiceDefaults стоїть першим, щоб його бачили всі логи запиту, зокрема лог обробника помилок
app.UseCorrelationId();

// У .NET 10 middleware сам не логує винятки, які обробив IExceptionHandler, —
// тож кожен виняток потрапляє в лог рівно один раз, з DomainExceptionHandler
app.UseExceptionHandler();

app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostic, http) =>
{
    diagnostic.Set("CorrelationId", http.Items[CorrelationIdMiddleware.HeaderName]);
    diagnostic.Set("UserId", http.User.Identity?.Name ?? "anonymous");
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGrpcService<CoursesGrpcService>();   // gRPC-API каталогу для Aggregator
app.MapDefaultEndpoints();   // /health і /alive

app.Run();

// Потрібно для WebApplicationFactory<Program> в API-тестах
public partial class Program;
