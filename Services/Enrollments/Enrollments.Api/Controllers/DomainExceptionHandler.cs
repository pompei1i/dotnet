using Enrollments.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

/// <summary>
/// Єдиний глобальний обробник винятків — остання ланка ланцюжка
/// «процедура → DAL → BLL → HTTP-статус». Контролери не містять try/catch:
/// NotFound → 404, BusinessConflict → 409, Validation → 400, усе інше → 500.
/// Відповідь завжди у форматі ProblemDetails.
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            BusinessConflictException => (StatusCodes.Status409Conflict, "Business rule conflict"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning("{ExceptionType}: {Message}", exception.GetType().Name, exception.Message);

        http.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Текст доменного винятку безпечний для клієнта; деталі несподіваної
                // помилки (SQL, стек) назовні не віддаються — вони лише в лозі.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred. Use traceId to find it in the logs."
                    : exception.Message
            }
        });
    }
}
