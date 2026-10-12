namespace Enrollments.Domain.Exceptions;

/// <summary>Запитаний ресурс не існує. API повертає 404 Not Found.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
