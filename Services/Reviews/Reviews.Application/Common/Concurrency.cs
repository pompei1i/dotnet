using Reviews.Domain.Exceptions;

namespace Reviews.Application.Common;

/// <summary>
/// Перевірка очікуваної версії з заголовка If-Match ще до зміни сутності. Вона ловить застарілі
/// дані клієнта; одночасний запис двох обробників ловить уже репозиторій фільтром за версією.
/// </summary>
public static class Concurrency
{
    public static void EnsureVersion(int actualVersion, int? expectedVersion, string entityName, string id)
    {
        if (expectedVersion is { } expected && expected != actualVersion)
            throw new ConflictException(
                $"{entityName} {id} has version {actualVersion}, but version {expected} was expected; reload it and retry");
    }
}
