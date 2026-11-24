using System.Diagnostics;
using MediatR;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;

namespace Reviews.WebApi.Metrics;

/// <summary>
/// Записує бізнес-метрики для кожної команди й запиту MediatR (Лаб. 07): тривалість — і для успішних,
/// і для невдалих (валідація, NotFound, збій бази), лічильники — лише після успішного збереження.
/// Реєструється у WebApi після behaviors Application-шару, тож стоїть найближче до обробника:
/// попадання в кеш (CachingBehavior) сюди не доходять і міряються окремо метриками кешу.
/// </summary>
public sealed class MetricsBehavior<TRequest, TResponse>(ReviewMetrics metrics) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var operation = typeof(TRequest).Name;
        var isCommand = operation.EndsWith("Command", StringComparison.Ordinal);

        metrics.Started();
        var started = Stopwatch.GetTimestamp();
        var success = false;
        try
        {
            var response = await next(ct);
            success = true;
            CountBusinessEvent(request);
            return response;
        }
        finally
        {
            metrics.Finished(operation, isCommand, success, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private void CountBusinessEvent(TRequest request)
    {
        switch (request)
        {
            case CreateReviewCommand create: metrics.ReviewCreated(create.Rating); break;
            case UpdateReviewCommand: metrics.ReviewUpdated(); break;
            case DeleteReviewCommand: metrics.ReviewDeleted(); break;
            case AddCommentCommand comment: metrics.CommentAdded(comment.Role); break;
        }
    }
}
