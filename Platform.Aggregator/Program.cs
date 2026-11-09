using System.Text.Json.Serialization;
using Microsoft.Extensions.Http.Resilience;
using Platform.Aggregator.Clients;
using Platform.Aggregator.ErrorHandling;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery, resilience і CorrelationIdHandler
// для всіх HttpClient — з ServiceDefaults
builder.AddServiceDefaults();

// Typed HttpClients. Адреса — логічне ім'я ресурсу з AppHost; "https+http" означає
// «HTTPS, якщо сервіс його має, інакше HTTP». Реальний host:port підставляє service discovery.
builder.Services.AddHttpClient<EnrollmentsClient>(client => client.BaseAddress = new("https+http://enrollments-api"));
builder.Services.AddHttpClient<CoursesClient>(client => client.BaseAddress = new("https+http://courses-api"));
builder.Services.AddHttpClient<ReviewsClient>(client => client.BaseAddress = new("https+http://reviews-api"));

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
