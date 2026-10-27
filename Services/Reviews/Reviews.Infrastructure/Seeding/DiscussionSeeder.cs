using Microsoft.Extensions.Logging;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>Три теми з Лаб. 01: вирішене й відкрите питання та закріплене оголошення.</summary>
public sealed class DiscussionSeeder(MongoDbContext context, ILogger<DiscussionSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var (inserted, skipped) = await SeedData.InsertMissingAsync(
            context.Discussions, SeedData.Load<Discussion>("discussions.json"), ct);
        logger.LogInformation("Seeding {Collection}: {Inserted} inserted, {Skipped} already present",
            MongoDbContext.DiscussionsCollection, inserted, skipped);
    }
}
