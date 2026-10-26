using Courses.Api.Controllers;
using Courses.Api.Middleware;
using Courses.Bll.Mapping;
using Courses.Bll.Services;
using Courses.Bll.Validation;
using Courses.Dal;
using Courses.Dal.Seeding;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Рядок підключення лежить в appsettings.json і перевизначається змінною оточення
// ConnectionStrings__CoursesDb — саме так його передає .NET Aspire (Platform.AppHost).
var connectionString = builder.Configuration.GetConnectionString("CoursesDb")
    ?? throw new InvalidOperationException("Connection string 'CoursesDb' is missing");

// Логування: рівні, sinks (консоль і файл) та збагачення задано в appsettings.json, розділ "Serilog".
// Там же Microsoft.EntityFrameworkCore.Database.Command = Information — у лозі видно SQL кожного запиту.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Courses.Api"));

// DAL: DbContext і Unit of Work — Scoped, один контекст на HTTP-запит
builder.Services.AddDbContext<CoursesDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

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

// CorrelationId стоїть першим, щоб його бачили всі логи запиту, зокрема лог обробника помилок
app.UseMiddleware<CorrelationIdMiddleware>();

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

app.Run();

// Потрібно для WebApplicationFactory<Program> в API-тестах
public partial class Program;
