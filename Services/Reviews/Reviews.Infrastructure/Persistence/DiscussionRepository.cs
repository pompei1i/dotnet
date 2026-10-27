using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Запис тем обговорення через MongoDB Driver.</summary>
public sealed class DiscussionRepository(MongoDbContext context) : IDiscussionRepository
{
    private readonly IMongoCollection<Discussion> _discussions = context.Discussions;

    public async Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _discussions.Find(Builders<Discussion>.Filter.Eq(d => d.Id, id)).FirstOrDefaultAsync(ct);

    public Task AddAsync(Discussion discussion, CancellationToken ct = default)
        => _discussions.InsertOneAsync(discussion, cancellationToken: ct);

    public Task UpdateAsync(Discussion discussion, CancellationToken ct = default)
        => OptimisticConcurrency.ReplaceAsync(_discussions, discussion, "Discussion", ct);
}
