using Enrollments.Api.Controllers;
using Enrollments.Bll.Mapping;
using Enrollments.Bll.Services;
using Enrollments.Dal;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Рядок підключення лежить в appsettings.json і перевизначається змінною оточення
// ConnectionStrings__EnrollmentsDb — саме так його передає .NET Aspire (Platform.AppHost).
var connectionString = builder.Configuration.GetConnectionString("EnrollmentsDb")
    ?? throw new InvalidOperationException("Connection string 'EnrollmentsDb' is missing");

// Логування: Serilog читає рівні з конфігурації та пише в консоль
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// DAL: Unit of Work — Scoped, тобто один екземпляр і одне з'єднання на HTTP-запит
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

// BLL: сервіси
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// BLL: профілі AutoMapper реєструються централізовано.
// Ліцензійний ключ Community-редакції не зберігається в репозиторії: його задають змінною
// оточення AutoMapper__LicenseKey або через user-secrets. Без ключа застосунок працює,
// лише пише в лог попередження.
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<StudentProfile>();
    cfg.AddProfile<CourseProfile>();
    cfg.AddProfile<OrderProfile>();
});

builder.Services.AddControllers();

// Помилки — у форматі ProblemDetails (RFC 9457), винятки перетворює один глобальний обробник
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

// Потрібно для WebApplicationFactory<Program> в інтеграційних тестах
public partial class Program;
