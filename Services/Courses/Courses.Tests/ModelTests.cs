using Courses.Dal;
using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests;

/// <summary>
/// Перевірки моделі EF Core без бази: з'єднання не відкривається, модель будується з конфігурацій.
/// </summary>
public class ModelTests
{
    private static CoursesDbContext CreateContext()
        => new(new DbContextOptionsBuilder<CoursesDbContext>()
            .UseSqlServer("Server=localhost;Database=CoursesDb;Trusted_Connection=True")
            .Options);

    [Fact]
    public void Model_has_no_changes_missing_from_the_migrations()
    {
        using var context = CreateContext();

        // Якщо модель змінили, а міграцію не додали, тест впаде
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Join_entity_has_a_composite_key_and_its_own_fields()
    {
        using var context = CreateContext();
        var link = context.Model.FindEntityType(typeof(CourseCategory))!;

        Assert.Equal(["CourseId", "CategoryId"], link.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.NotNull(link.FindProperty(nameof(CourseCategory.AddedAt)));
        Assert.NotNull(link.FindProperty(nameof(CourseCategory.IsPrimary)));
    }

    [Fact]
    public void Generated_ddl_contains_the_constraints_and_indexes_from_lab_01()
    {
        using var context = CreateContext();

        var ddl = context.Database.GenerateCreateScript();

        Assert.Contains("CONSTRAINT [CK_Courses_Price] CHECK ([Price] >= 0)", ddl);
        Assert.Contains("CREATE UNIQUE INDEX [UQ_Courses_Slug]", ddl);
        Assert.Contains("CREATE UNIQUE INDEX [UX_CourseCategories_Primary]", ddl);
        Assert.Contains("[Price] decimal(18,2) NOT NULL", ddl);
        Assert.Contains("CONSTRAINT [PK_CourseCategories] PRIMARY KEY ([CourseId], [CategoryId])", ddl);
    }

    [Fact]
    public void Soft_deleted_courses_are_hidden_by_a_query_filter()
    {
        using var context = CreateContext();

        var sql = context.Courses.Where(course => course.Price > 0).ToQueryString();

        Assert.Contains("[IsDeleted] = CAST(0 AS bit)", sql);
    }
}
