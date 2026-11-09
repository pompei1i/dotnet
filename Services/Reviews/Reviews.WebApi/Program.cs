using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.WebApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи (Лаб. 05): Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Aspire передає рядок підключення до бази як ConnectionStrings:FeedbackDb, а Infrastructure
// читає розділ MongoDb. Без Aspire лишається резервний MongoDb:ConnectionString з appsettings.json.
if (builder.Configuration.GetConnectionString("FeedbackDb") is { Length: > 0 } feedbackDb)
    builder.Configuration["MongoDb:ConnectionString"] = feedbackDb;

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
app.MapDefaultEndpoints();   // /health (зокрема перевірка MongoDB) і /alive

app.Run();

// Потрібно для WebApplicationFactory<Program> в API-тестах
public partial class Program;
