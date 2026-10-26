using Courses.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Courses.Api.Controllers;

/// <summary>
/// Глобальна обробка помилок: один обробник на весь сервіс, контролери без try/catch.
/// EntityNotFound -> 404, Validation -> 400, Conflict і Domain -> 409, усе інше -> 500.
/// Відповідь — ProblemDetails; деталі несподіваної помилки показуються лише в Development.
/// Кожен виняток логується тут рівно один раз.
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict with existing data"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Request {Method} {Path} failed", http.Request.Method, http.Request.Path);
        else
            logger.LogWarning("Request {Method} {Path} rejected with {StatusCode}: {Reason}",
                http.Request.Method, http.Request.Path, status, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // Повідомлення доменних винятків безпечні для клієнта. Текст несподіваної помилки
            // (SQL, шляхи, стек) виходить назовні лише в Development
            Detail = status != StatusCodes.Status500InternalServerError || environment.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred. Use traceId to find it in the logs."
        };

        if (environment.IsDevelopment() && status == StatusCodes.Status500InternalServerError)
            problem.Extensions["exception"] = exception.ToString();

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
