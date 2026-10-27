namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Секція MongoDb з appsettings.{Environment}.json. Пул з'єднань і таймаути задаються
/// параметрами рядка підключення (maxPoolSize, serverSelectionTimeoutMS тощо).
/// </summary>
public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "";

    public string DatabaseName { get; set; } = "";

    /// <summary>Створювати індекси під час старту (повторне створення наявного індексу безпечне).</summary>
    public bool CreateIndexesOnStartup { get; set; } = true;

    /// <summary>Додавати початкові дані під час старту; у Production вимкнено в appsettings.Production.json.</summary>
    public bool SeedOnStartup { get; set; } = true;
}
