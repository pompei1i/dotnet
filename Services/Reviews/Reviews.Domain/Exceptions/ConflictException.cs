namespace Reviews.Domain.Exceptions;

/// <summary>
/// Зміна конфліктує з поточним станом даних — насамперед документ уже змінив хтось інший
/// і його версія не збігається з очікуваною (HTTP 409).
/// </summary>
public class ConflictException(string message) : Exception(message);
