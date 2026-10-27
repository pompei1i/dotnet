namespace Courses.Domain.Exceptions;

/// <summary>Запитаної сутності не існує. API повертає 404 Not Found.</summary>
public class EntityNotFoundException(string entityName, object id)
    : Exception($"{entityName} {id} not found")
{
    public string EntityName { get; } = entityName;
    public object Id { get; } = id;
}
