using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Platform.ServiceDefaults;
using Polly;

namespace Platform.Aggregator.Resilience;

/// <summary>
/// Реєстрація gRPC-клієнта з політикою стійкості конкретного сервісу (Лаб. 07).
///
/// 1. Стандартний обробник із ServiceDefaults (ConfigureHttpClientDefaults) спершу знімається —
///    RemoveAllResilienceHandlers, — і лише потім додається власний: обробники не нашаровуються.
/// 2. Retry HTTP-обробника за замовчуванням вимкнено для POST (ServiceDefaults), а кожен gRPC-виклик —
///    це POST. Усі RPC у контрактах платформи лише читають (GetOrder, GetCoursesByIds...), тобто
///    ідемпотентні, тому для gRPC-клієнтів повтори свідомо увімкнено назад.
/// 3. HTTP-обробник повторює лише мережеві збої й таймаути: gRPC-помилки (UNAVAILABLE...) приходять
///    статусом у трейлерах відповіді з HTTP 200. Для них — вбудована RetryPolicy gRPC-клієнта.
/// 4. Переходи circuit breaker логуються: Open — Error, HalfOpen — Warning, Closed — Information.
/// </summary>
public static class ResilientGrpcClientExtensions
{
    public static IHttpClientBuilder AddResilientGrpcClient<TClient>(
        this IServiceCollection services, string address, ServiceResiliencePolicy policy)
        where TClient : class
    {
        var builder = services
            .AddGrpcClient<TClient>(options => options.Address = new Uri(address))
            .ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
            {
                MethodConfigs =
                {
                    new MethodConfig
                    {
                        Names = { MethodName.Default },   // усі методи сервісу
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = policy.GrpcMaxAttempts,
                            InitialBackoff = policy.GrpcInitialBackoff,
                            MaxBackoff = policy.GrpcMaxBackoff,
                            BackoffMultiplier = 2,
                            // Тимчасові стани: сервіс перезапускається / перевантажений. NotFound, InvalidArgument
                            // від повтору не зміняться, Internal — баг на сервері, тож не повторюються
                            RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted }
                        }
                    }
                }
            });

        builder.RemoveAllResilienceHandlers();

        builder.AddStandardResilienceHandler().Configure((options, provider) =>
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger($"Platform.Aggregator.Resilience.{policy.Service}");

            options.TotalRequestTimeout.Timeout = policy.TotalRequestTimeout;
            options.AttemptTimeout.Timeout = policy.AttemptTimeout;

            options.Retry.MaxRetryAttempts = policy.HttpRetries;
            options.Retry.Delay = policy.RetryDelay;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;   // повтори різних запитів не збігаються в часі (thundering herd)

            options.CircuitBreaker.FailureRatio = policy.BreakerFailureRatio;
            options.CircuitBreaker.MinimumThroughput = policy.BreakerMinimumThroughput;   // навчальний стенд: мало запитів
            options.CircuitBreaker.SamplingDuration = policy.BreakerSamplingDuration;
            options.CircuitBreaker.BreakDuration = policy.BreakDuration;

            // Polly v8 виконує колбеки переходів у власному фоновому планувальнику, створеному з контекстом
            // ПЕРШОГО запиту через цей pipeline. Тому CorrelationId береться з HTTP-запиту, що спричинив
            // перехід, а ambient-Activity чужого запиту прибирається, щоб лог не потрапив у чужий trace
            options.CircuitBreaker.OnOpened = args => LogTransition(args.Context, () =>
                logger.LogError("Circuit to {Service} opened for {BreakDuration}: {FailureReason} — calls fail fast without waiting (CorrelationId {CorrelationId})",
                    policy.Service, args.BreakDuration, Describe(args.Outcome), CorrelationIdOf(args.Context)));
            options.CircuitBreaker.OnHalfOpened = args => LogTransition(args.Context, () =>
                logger.LogWarning("Circuit to {Service} half-open: letting a trial call through (CorrelationId {CorrelationId})",
                    policy.Service, CorrelationIdOf(args.Context)));
            options.CircuitBreaker.OnClosed = args => LogTransition(args.Context, () =>
                logger.LogInformation("Circuit to {Service} closed: the service is responding again (CorrelationId {CorrelationId})",
                    policy.Service, CorrelationIdOf(args.Context)));
        });

        return builder;
    }

    private static ValueTask LogTransition(ResilienceContext context, Action log)
    {
        var ambient = Activity.Current;
        Activity.Current = null;
        try
        {
            log();
        }
        finally
        {
            Activity.Current = ambient;
        }
        return ValueTask.CompletedTask;
    }

    private static string CorrelationIdOf(ResilienceContext context)
        => context.GetRequestMessage()?.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values) == true
            ? values.First()
            : "unknown";

    private static string Describe(Outcome<HttpResponseMessage> outcome)
        => outcome.Exception is { } exception
            ? exception.GetType().Name
            : $"HTTP {(int?)outcome.Result?.StatusCode}";
}
