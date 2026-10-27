namespace Courses.Domain.Exceptions;

/// <summary>
/// Операція суперечить даним у базі: дубль унікального значення (Slug, Email),
/// конфлікт версій рядка. API повертає 409 Conflict.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, Exception innerException) : base(message, innerException) { }
}
