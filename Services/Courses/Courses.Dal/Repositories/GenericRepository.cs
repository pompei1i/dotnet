using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Courses.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Courses.Dal.Repositories;

/// <summary>
/// Async Generic Repository на DbSet&lt;T&gt;. Спільний CRUD написано один раз; конкретні репозиторії
/// додають лише свої запити. SaveChanges тут свідомо немає — це робота Unit of Work.
/// </summary>
public class GenericRepository<T>(CoursesDbContext context) : IGenericRepository<T> where T : class
{
    protected readonly CoursesDbContext Context = context;
    protected readonly DbSet<T> Set = context.Set<T>();

    public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    /// <summary>
    /// Репозиторій не знає, що саме фільтрується: WithSpecification перетворює критерії,
    /// Include, сортування і Skip/Take специфікації на один LINQ-вираз, а отже — один SQL-запит.
    /// </summary>
    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default)
        => await Set.WithSpecification(specification).ToListAsync(ct);

    /// <summary>Лише критерії Where специфікації — без Include, сортування і сторінки: рахує сама база.</summary>
    public Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default)
        => SpecificationEvaluator.Default
            .GetQuery(Set.AsQueryable(), specification, evaluateCriteriaOnly: true)
            .CountAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Delete(T entity) => Set.Remove(entity);
}
