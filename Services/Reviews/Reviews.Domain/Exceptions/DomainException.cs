namespace Reviews.Domain.Exceptions;

/// <summary>Порушення бізнес-правила сутності або Value Object (HTTP 400).</summary>
public class DomainException(string message) : Exception(message);
