namespace Enrollments.Domain.Exceptions;

/// <summary>
/// Вхідні дані порушують бізнес-правило, яке не можна перевірити атрибутами на DTO.
/// API повертає 400 Bad Request.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }

    public ValidationException(string message, Exception innerException) : base(message, innerException) { }
}
