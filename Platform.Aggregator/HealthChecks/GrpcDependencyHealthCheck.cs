using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Перевірка gRPC-залежності Aggregator стандартним gRPC Health Checking Protocol (grpc.health.v1.Health/Check).
/// Сервіс відповідає на основі власних readiness-перевірок (база, Redis), тож Aggregator бачить реальний
/// стан, а не лише те, що процес слухає порт.
///
/// - Короткий deadline (2 с): недоступність має виявлятися швидко, health check опитують кожні кілька секунд.
/// - Health-клієнт свідомо без повторів і circuit breaker (RemoveAllResilienceHandlers): перевірка має
///   бачити справжній стан залежності, а не «вилікуваний» повторами.
/// - Check лише читає стан — побічних ефектів у сервісі немає.
/// - Відмова -> статус з реєстрації: Unhealthy для критичних (enrollments, courses), Degraded для reviews —
///   без відгуків Aggregator працює, лише з частковими відповідями.
/// </summary>
public sealed class GrpcDependencyHealthCheck(
    GrpcClientFactory clients,
    ILogger<GrpcDependencyHealthCheck> logger,
    string service) : IHealthCheck
{
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var client = clients.CreateClient<Health.HealthClient>(service);
        try
        {
            var reply = await client.CheckAsync(new HealthCheckRequest(),
                deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{service} is serving")
                : new HealthCheckResult(context.Registration.FailureStatus, $"{service} status: {reply.Status}");
        }
        catch (RpcException exception)
        {
            logger.LogWarning("gRPC health check of {Service} failed: {GrpcStatus} {GrpcDetail}",
                service, exception.StatusCode, exception.Status.Detail);
            return new HealthCheckResult(context.Registration.FailureStatus,
                $"{service} is unavailable: {exception.StatusCode}", exception);
        }
    }
}
