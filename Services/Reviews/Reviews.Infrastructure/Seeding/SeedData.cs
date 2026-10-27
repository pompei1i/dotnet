using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Reviews.Domain.Common;

namespace Reviews.Infrastructure.Seeding;

/// <summary>
/// Початкові дані лежать у Seeding/Data/*.json (MongoDB Extended JSON: $oid, $date), вбудованих
/// у збірку. Документи ті самі, що в db/p3/seed.js з Лаб. 01, зі сталими _id — тож сідер
/// і скрипт Лаб. 01 не створюють дублікатів один одному.
/// </summary>
internal static class SeedData
{
    /// <summary>Читає масив documents із JSON-ресурсу й десеріалізує його в сутності через BsonClassMap.</summary>
    public static IReadOnlyList<TEntity> Load<TEntity>(string fileName)
    {
        var assembly = typeof(SeedData).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Seeding.Data.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded seed file {resourceName} is missing");
        using var reader = new StreamReader(stream);

        return BsonDocument.Parse(reader.ReadToEnd())["documents"].AsBsonArray
            .Select(document => BsonSerializer.Deserialize<TEntity>(document.AsBsonDocument))
            .ToList();
    }

    /// <summary>
    /// Ідемпотентна вставка: одним запитом з'ясовує, які _id уже є, і вставляє решту одним
    /// InsertMany. Наявні документи не перезаписуються.
    /// </summary>
    public static async Task<(int Inserted, int Skipped)> InsertMissingAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        IReadOnlyList<TEntity> documents,
        CancellationToken ct)
        where TEntity : BaseEntity
    {
        var ids = documents.Select(document => document.Id).ToList();
        var existing = await collection
            .Find(Builders<TEntity>.Filter.In(document => document.Id, ids))
            .Project(document => document.Id)
            .ToListAsync(ct);

        var missing = documents.Where(document => !existing.Contains(document.Id)).ToList();
        if (missing.Count > 0)
            await collection.InsertManyAsync(missing, new InsertManyOptions { IsOrdered = false }, ct);

        return (missing.Count, documents.Count - missing.Count);
    }
}
