namespace Reviews.Infrastructure.Seeding;

/// <summary>Наповнює одну колекцію початковими даними; повторний запуск не створює дублікатів.</summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
