using System.Diagnostics;
using MediatR;
using Reviews.Application.Common.Diagnostics;

namespace Reviews.Application.Common.Behaviors;

/// <summary>
/// Найзовнішніший behavior (Лаб. 05): кожна команда й запит MediatR стає окремим span
/// "Reviews CreateReviewCommand" тощо. Усередині нього — spans команд MongoDB, тож у trace
/// видно, яка бізнес-операція скільки звертань до бази зробила. Обробник може додати
/// власні атрибути через Activity.Current (наприклад, review.id).
/// </summary>
public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;

        using var activity = ReviewsTelemetry.Source.StartActivity($"Reviews {requestName}");
        activity?.SetTag("mediatr.request", requestName);
        activity?.SetTag("mediatr.kind", requestName.EndsWith("Query", StringComparison.Ordinal) ? "query" : "command");

        try
        {
            var response = await next(ct);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception exception)
        {
            // Тип і причина відмови — в атрибутах span; HTTP-статус формує обробник у WebApi
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.SetTag("error.type", exception.GetType().Name);
            throw;
        }
    }
}
