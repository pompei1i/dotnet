using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Точка доступу до бази feedback_db. Один екземпляр на застосунок (Singleton): MongoClient
/// потокобезпечний і сам тримає пул з'єднань. Колекції — ті самі, що створено в Лаб. 01.
/// </summary>
public sealed class MongoDbContext
{
    public const string ReviewsCollection = "reviews";
    public const string DiscussionsCollection = "discussions";

    public MongoDbContext(IOptions<MongoDbOptions> options, ILoggerFactory loggerFactory)
    {
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);

        // Драйвер пише власні логи через ILogger: категорія MongoDB.Command на рівні Debug
        // показує кожну команду ($match, $skip, $limit, update з фільтром за version)
        settings.LoggingSettings = new LoggingSettings(loggerFactory);

        Client = new MongoClient(settings);
        Database = Client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(ReviewsCollection);

    public IMongoCollection<Discussion> Discussions => Database.GetCollection<Discussion>(DiscussionsCollection);
}
