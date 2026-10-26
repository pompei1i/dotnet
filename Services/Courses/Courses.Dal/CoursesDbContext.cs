using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courses.Dal;

/// <summary>
/// Контекст бази CoursesDb (Проєкт №2 з Лаб. 01). Схему створюють міграції з цієї моделі
/// (Code First); конфігурація кожної сутності лежить в окремому класі в папці Configurations.
/// </summary>
public class CoursesDbContext(DbContextOptions<CoursesDbContext> options) : DbContext(options)
{
    // Автентифікації ще немає, тому в аудитні колонки записується назва сервісу
    private const string Actor = "courses-api";

    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseDetail> CourseDetails => Set<CourseDetail>();
    public DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();
    public DbSet<Lesson> Lessons => Set<Lesson>();

    /// <summary>Усі IEntityTypeConfiguration&lt;T&gt; цієї збірки підключаються централізовано, одним викликом.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoursesDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditColumns();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Заповнює аудитні колонки перед збереженням. CreatedAt ставить сама база (DEFAULT SYSUTCDATETIME()),
    /// решту — контекст, тому в сутностях ці властивості мають лише приватні setter-и.
    /// </summary>
    private void StampAuditColumns()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedBy") is not null)
                entry.Property("CreatedBy").CurrentValue ??= Actor;

            if (entry.State != EntityState.Modified)
                continue;

            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = now;
            if (entry.Metadata.FindProperty("UpdatedBy") is not null)
                entry.Property("UpdatedBy").CurrentValue = Actor;
        }
    }
}
