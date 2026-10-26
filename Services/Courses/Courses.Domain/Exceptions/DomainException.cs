namespace Courses.Domain.Exceptions;

/// <summary>
/// Сутність відмовилась виконати операцію, бо та порушує її власне правило
/// (від'ємна ціна, публікація курсу без уроків). API повертає 409 Conflict.
/// </summary>
public class DomainException(string message) : Exception(message);
