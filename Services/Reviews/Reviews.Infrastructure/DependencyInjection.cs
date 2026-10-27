using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Реєструє шар Infrastructure: BSON-мапінг, налаштування й контекст MongoDB, реалізації
    /// репозиторіїв та інтерфейсів читання, створення індексів і сідінг під час старту, health check.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Мапінг — до першого звернення до бази й до побудови будь-якого серіалізатора
        MongoMappings.Register();

        services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "MongoDb:ConnectionString is required")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName), "MongoDb:DatabaseName is required")
            .ValidateOnStart();

        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<IDiscussionQueries, DiscussionQueries>();

        // Порядок важливий: спершу індекси (унікальність), потім дані
        services.AddSingleton<IDataSeeder, ReviewSeeder>();
        services.AddSingleton<IDataSeeder, DiscussionSeeder>();
        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<DataSeedingService>();

        services.AddHealthChecks().AddMongoDb(
            clientFactory: sp => sp.GetRequiredService<MongoDbContext>().Client,
            databaseNameFactory: sp => sp.GetRequiredService<IOptions<MongoDbOptions>>().Value.DatabaseName,
            name: "mongodb",
            tags: ["ready"]);

        return services;
    }
}
