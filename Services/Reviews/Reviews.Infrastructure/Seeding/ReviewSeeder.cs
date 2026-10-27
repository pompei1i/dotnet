using Microsoft.Extensions.Logging;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>Чотири відгуки з Лаб. 01 — з різними наборами полів (гнучка схема).</summary>
public sealed class ReviewSeeder(MongoDbContext context, ILogger<ReviewSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var (inserted, skipped) = await SeedData.InsertMissingAsync(context.Reviews, SeedData.Load<Review>("reviews.json"), ct);
        logger.LogInformation("Seeding {Collection}: {Inserted} inserted, {Skipped} already present",
            MongoDbContext.ReviewsCollection, inserted, skipped);
    }
}
