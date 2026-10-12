namespace Enrollments.Domain.Exceptions;

/// <summary>
/// Операція суперечить поточному стану даних: недопустимий перехід статусу, дубль унікального
/// значення, конфлікт версій. API повертає 409 Conflict.
/// </summary>
public class BusinessConflictException : Exception
{
    public BusinessConflictException(string message) : base(message) { }

    public BusinessConflictException(string message, Exception innerException) : base(message, innerException) { }
}
