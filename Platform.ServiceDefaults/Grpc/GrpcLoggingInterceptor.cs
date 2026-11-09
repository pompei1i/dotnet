using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Platform.ServiceDefaults.Grpc;

/// <summary>
/// Серверний interceptor для всіх gRPC-сервісів платформи (AddGrpc(o => o.Interceptors.Add&lt;GrpcLoggingInterceptor&gt;())).
/// Логує кожен вхідний виклик структурованими властивостями — GrpcMethod, GrpcPeer, GrpcStatus, ElapsedMs —
/// а не конкатенацією рядків, тож у Structured logs за ними можна фільтрувати. CorrelationId і RequestPath
/// у цих записах — від CorrelationIdMiddleware: gRPC-запит проходить той самий конвеєр ASP.NET Core,
/// а метадані gRPC — це заголовки HTTP/2, тож x-correlation-id приходить як звичайний заголовок.
///
/// Атрибути rpc.* з'являються і в span запиту, який створює інструментація ASP.NET Core: так у trace
/// видно сервіс, метод і код статусу gRPC (сам HTTP-статус gRPC-відповіді завжди 200).
/// </summary>
public sealed class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        TagSpan(context);
        try
        {
            var response = await continuation(request, context);
            Completed(context, StatusCode.OK, started);
            return response;
        }
        catch (RpcException exception)
        {
            // Очікувані відмови (NotFound, InvalidArgument...) — Warning без stack trace
            Failed(context, exception.StatusCode, started, exception, expected: exception.StatusCode != StatusCode.Internal);
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Клієнт скасував виклик або минув його deadline
            Completed(context, context.Deadline < DateTime.UtcNow ? StatusCode.DeadlineExceeded : StatusCode.Cancelled, started);
            throw;
        }
        catch (Exception exception)
        {
            // Несподівана помилка: повний stack trace у лог, а клієнту — Internal без деталей реалізації
            Failed(context, StatusCode.Internal, started, exception, expected: false);
            throw new RpcException(new Status(StatusCode.Internal, "Unexpected server error"));
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var started = Stopwatch.GetTimestamp();
        TagSpan(context);
        try
        {
            await continuation(request, responseStream, context);
            Completed(context, StatusCode.OK, started);
        }
        catch (RpcException exception)
        {
            Failed(context, exception.StatusCode, started, exception, expected: exception.StatusCode != StatusCode.Internal);
            throw;
        }
    }

    private static void TagSpan(ServerCallContext context)
    {
        // context.Method — "/courses.CoursesService/GetCourse"
        var parts = context.Method.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var activity = Activity.Current;
        activity?.SetTag("rpc.system", "grpc");
        if (parts.Length == 2)
        {
            activity?.SetTag("rpc.service", parts[0]);
            activity?.SetTag("rpc.method", parts[1]);
        }
    }

    private void Completed(ServerCallContext context, StatusCode status, long started)
    {
        Activity.Current?.SetTag("rpc.grpc.status_code", (int)status);
        logger.LogInformation("gRPC {GrpcMethod} from {GrpcPeer}: {GrpcStatus} in {ElapsedMs} ms",
            context.Method, context.Peer, status, ElapsedMs(started));
    }

    private void Failed(ServerCallContext context, StatusCode status, long started, Exception exception, bool expected)
    {
        var activity = Activity.Current;
        activity?.SetTag("rpc.grpc.status_code", (int)status);
        activity?.SetStatus(ActivityStatusCode.Error, status.ToString());

        if (expected)
            logger.LogWarning("gRPC {GrpcMethod} from {GrpcPeer}: {GrpcStatus} in {ElapsedMs} ms — {GrpcDetail}",
                context.Method, context.Peer, status, ElapsedMs(started), (exception as RpcException)?.Status.Detail);
        else
            logger.LogError(exception, "gRPC {GrpcMethod} from {GrpcPeer}: {GrpcStatus} in {ElapsedMs} ms",
                context.Method, context.Peer, status, ElapsedMs(started));
    }

    private static double ElapsedMs(long started) => Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1);
}
