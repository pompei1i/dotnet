using MongoDB.Bson;

namespace Reviews.Domain.Common;

/// <summary>
/// Базова сутність документа MongoDB: ідентифікатор, час створення й зміни та версія
/// для оптимістичної конкурентності. Сетери захищені — стан змінюють лише методи сутностей.
/// </summary>
public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>
    /// Версія документа. Новий документ має версію 1 — так само, як у валідаторі
    /// $jsonSchema колекції reviews з Лаб. 01 (version: int, minimum 1).
    /// </summary>
    public int Version { get; protected set; } = 1;

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Викликає репозиторій, коли записує зміну: нова версія = очікувана + 1.</summary>
    public void BumpVersion() => Version++;
}
