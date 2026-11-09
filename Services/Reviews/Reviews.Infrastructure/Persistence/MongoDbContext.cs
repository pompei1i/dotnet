using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
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

    private static readonly HashSet<string> HandshakeCommands =
        new(["isMaster", "ismaster", "hello", "saslStart", "saslContinue", "buildInfo", "getLastError"], StringComparer.Ordinal);

    public MongoDbContext(IOptions<MongoDbOptions> options, ILoggerFactory loggerFactory)
    {
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);

        // Драйвер пише власні логи через ILogger: категорія MongoDB.Command на рівні Debug
        // показує кожну команду ($match, $skip, $limit, update з фільтром за version)
        settings.LoggingSettings = new LoggingSettings(loggerFactory);

        // Трасування (Лаб. 05): кожна команда драйвера стає span OpenTelemetry з назвою колекції
        // й операції. Текст команди не записується — у ньому можуть бути дані користувачів.
        // Службові команди рукостискання й автентифікації нових з'єднань лише засмічували б trace.
        settings.ClusterConfigurator = cluster => cluster.Subscribe(
            new DiagnosticsActivityEventSubscriber(new InstrumentationOptions
            {
                CaptureCommandText = false,
                ShouldStartActivity = command => !HandshakeCommands.Contains(command.CommandName)
            }));

        Client = new MongoClient(settings);
        Database = Client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(ReviewsCollection);

    public IMongoCollection<Discussion> Discussions => Database.GetCollection<Discussion>(DiscussionsCollection);
}
