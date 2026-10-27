using MongoDB.Driver;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Оптимістична конкурентність через поле version: документ замінюється лише тоді, коли в базі
/// досі та версія, яку прочитав обробник. Перевірка й запис — одна атомарна операція
/// ReplaceOne, тож з двох одночасних змін одна отримає ConflictException.
/// </summary>
internal static class OptimisticConcurrency
{
    public static async Task ReplaceAsync<TEntity>(
        IMongoCollection<TEntity> collection,
        TEntity entity,
        string entityName,
        CancellationToken ct)
        where TEntity : BaseEntity
    {
        var expectedVersion = entity.Version;
        var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id) & VersionFilter<TEntity>(expectedVersion);

        entity.BumpVersion();
        var result = await collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new ConflictException(
                $"{entityName} {entity.Id} was modified by someone else (expected version {expectedVersion}); reload it and retry");
    }

    /// <summary>
    /// Документи, створені скриптом Лаб. 01 без поля version (теми discussions), сутність читає
    /// як версію 1 — для неї фільтр приймає і version: 1, і відсутнє поле.
    /// </summary>
    internal static FilterDefinition<TEntity> VersionFilter<TEntity>(int expectedVersion)
        where TEntity : BaseEntity
    {
        var filter = Builders<TEntity>.Filter;
        return expectedVersion == 1
            ? filter.Eq(e => e.Version, 1) | filter.Exists(e => e.Version, exists: false)
            : filter.Eq(e => e.Version, expectedVersion);
    }
}
