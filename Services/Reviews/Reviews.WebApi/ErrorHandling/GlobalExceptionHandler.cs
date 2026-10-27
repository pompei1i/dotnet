using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Reviews.Application.Common.Exceptions;
using Reviews.Domain.Exceptions;

namespace Reviews.WebApi.ErrorHandling;

/// <summary>
/// Глобальна обробка помилок: доменні, валідаційні й MongoDB-винятки -> ProblemDetails
/// з відповідним статусом. Контролери й обробники не містять try/catch.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const int StatusCodeClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        // Клієнт сам розірвав з'єднання — це не збій сервера; відповідь уже ніхто не прочитає
        if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            http.Response.StatusCode = StatusCodeClientClosedRequest;
            return true;
        }

        // Усі помилки FluentValidation з ValidationBehavior — разом, згруповані за полем
        if (exception is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

            return await WriteAsync(http, exception, new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            });
        }

        var (status, title) = Classify(exception);

        // 4xx уже залогував LoggingBehavior (причина відмови); тут — лише збої сервера й бази
        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Request {Method} {Path} failed with {StatusCode}",
                http.Request.Method, http.Request.Path, status);
        else
            logger.LogDebug("Request {Method} {Path} rejected with {StatusCode}: {Reason}",
                http.Request.Method, http.Request.Path, status, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = DetailFor(exception, status)
        };
        if (environment.IsDevelopment() && status == StatusCodes.Status500InternalServerError)
            problem.Extensions["exception"] = exception.ToString();

        return await WriteAsync(http, exception, problem);
    }

    /// <summary>Відповідність винятків HTTP-статусам (таблиця 2 методички).</summary>
    internal static (int Status, string Title) Classify(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict with the current state of the resource"),
        DomainException => (StatusCodes.Status400BadRequest, "Business rule violated"),

        // Унікальний індекс (наприклад, другий відгук студента на курс) -> 409; інші помилки запису -> 400
        MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
            => (StatusCodes.Status409Conflict, "Duplicate key"),
        MongoBulkWriteException bulk when bulk.WriteErrors.Count > 0
                                          && bulk.WriteErrors.All(error => error.Category == ServerErrorCategory.DuplicateKey)
            => (StatusCodes.Status409Conflict, "Duplicate key"),
        MongoWriteException or MongoBulkWriteException
            => (StatusCodes.Status400BadRequest, "Write rejected by the database"),

        // База недоступна: немає з'єднання або сервер не вибрано за serverSelectionTimeoutMS
        MongoConnectionException or TimeoutException or MongoExecutionTimeoutException
            => (StatusCodes.Status503ServiceUnavailable, "Database unavailable"),

        _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
    };

    /// <summary>
    /// Повідомлення доменних винятків безпечні для клієнта. Тексти винятків драйвера містять
    /// адреси серверів і дані документа, тож назовні вони йдуть лише в Development.
    /// </summary>
    private string DetailFor(Exception exception, int status)
    {
        if (exception is NotFoundException or ConflictException or DomainException || environment.IsDevelopment())
            return exception.Message;

        return status switch
        {
            StatusCodes.Status409Conflict => "A document with the same unique key already exists.",
            StatusCodes.Status400BadRequest => "The database rejected the document.",
            StatusCodes.Status503ServiceUnavailable => "The database is temporarily unavailable. Try again later.",
            _ => "An unexpected error occurred. Use traceId to find it in the logs."
        };
    }

    private async ValueTask<bool> WriteAsync(HttpContext http, Exception exception, ProblemDetails problem)
    {
        http.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (problem.Status == StatusCodes.Status503ServiceUnavailable)
            http.Response.Headers.RetryAfter = "5";

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
