using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>
/// Запускає всі IDataSeeder під час старту — після IndexCreationService (порядок реєстрації),
/// тож унікальні індекси вже діють. Вимикається MongoDb:SeedOnStartup.
/// </summary>
public sealed class DataSeedingService(IEnumerable<IDataSeeder> seeders, IOptions<MongoDbOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!options.Value.SeedOnStartup)
            return;

        foreach (var seeder in seeders)
            await seeder.SeedAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
