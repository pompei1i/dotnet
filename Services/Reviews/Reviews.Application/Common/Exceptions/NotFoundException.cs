namespace Reviews.Application.Common.Exceptions;

/// <summary>Документ із таким Id не знайдено або його видалено (HTTP 404).</summary>
public class NotFoundException(string entityName, object key)
    : Exception($"{entityName} {key} not found");
