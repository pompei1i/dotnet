namespace Courses.Domain.Exceptions;

/// <summary>
/// Вхідні дані порушують правило, яке можна перевірити лише з даними бази
/// (наприклад, основна категорія не входить до переданих категорій). API повертає 400.
/// </summary>
public class ValidationException(string message) : Exception(message);
