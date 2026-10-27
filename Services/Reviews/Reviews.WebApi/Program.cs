using Reviews.Application;
using Reviews.Infrastructure;
using Reviews.WebApi;
using Reviews.WebApi.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Логування: рівні, sinks (консоль і файл) та збагачення — в appsettings.json, розділ "Serilog"
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Reviews.WebApi"));

// Кожен шар реєструє себе сам; WebApi знає про Infrastructure лише тут, під час старту
builder.Services
    .AddApplication(builder.Configuration)       // MediatR, behaviors, валідатори
    .AddInfrastructure(builder.Configuration)    // MongoDB: мапінг, репозиторії, індекси, сідінг, health check
    .AddWebApi(builder.Configuration);           // контролери, AutoMapper, ProblemDetails, Swagger

var app = builder.Build();

// CorrelationId стоїть першим, щоб його бачили всі логи запиту, зокрема логи behaviors
app.UseMiddleware<CorrelationIdMiddleware>();

// Підсумковий запис про запит — зовні обробника винятків: він бачить уже остаточний статус
// (400, 404, 409...) і пише його на рівні Information, а не як необроблену помилку
app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostic, http) =>
{
    diagnostic.Set("CorrelationId", http.Items[CorrelationIdMiddleware.HeaderName]);
    diagnostic.Set("UserId", http.User.Identity?.Name ?? "anonymous");
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Потрібно для WebApplicationFactory<Program> в API-тестах
public partial class Program;
