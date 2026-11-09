using Platform.ApiGateway.Logging;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks і service discovery — як у кожному сервісі платформи
builder.AddServiceDefaults();

// YARP: маршрути й кластери — у розділі ReverseProxy файлу appsettings.json. Адреси кластерів —
// логічні імена ресурсів AppHost (http://enrollments-api), реальні адреси підставляє service discovery
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

builder.Services.AddSingleton<GatewayMetrics>();

var app = builder.Build();

// 1. CorrelationId — ДО MapReverseProxy: Gateway — перша точка входу, тут ідентифікатор
//    генерується, записується в Request.Headers, і YARP копіює його в проксований запит
app.UseCorrelationId();

// 2. Підсумок кожного запиту: маршрут, кластер, адреса сервісу, статус, тривалість, розмір, IP клієнта
app.UseMiddleware<GatewayLoggingMiddleware>();

app.MapDefaultEndpoints();

// 3. Проксі. Власний конвеєр YARP: після вибору екземпляра (load balancing) логується
//    рішення про маршрутизацію; стандартні кроки (affinity, balancing, passive health) — на місці
app.MapReverseProxy(proxy =>
{
    proxy.UseSessionAffinity();
    proxy.UseLoadBalancing();
    proxy.UsePassiveHealthChecks();
    proxy.UseMiddleware<RoutingDecisionLogger>();
});

app.Run();
