using Courses.Bll.Dtos;
using Courses.Bll.Specifications;
using Courses.Dal;
using Courses.Dal.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Courses.Tests;

/// <summary>
/// Чи перекладає EF Core кожен запит репозиторіїв у SQL — без бази даних.
/// Переклад відбувається ДО відкриття з'єднання, тож запит до неіснуючого сервера має впасти
/// саме на з'єднанні (SqlException). Неперекладний LINQ падає раніше з InvalidOperationException.
/// </summary>
public class QueryTranslationTests
{
    private static CoursesDbContext CreateContext()
        => new(new DbContextOptionsBuilder<CoursesDbContext>()
            .UseSqlServer("Server=tcp:127.0.0.1,1;Database=CoursesDb;User Id=test;Password=test;Connect Timeout=1;Encrypt=False")
            .Options);

    private static async Task AssertTranslatesToSql(Func<CoursesDbContext, Task> query)
    {
        await using var context = CreateContext();

        var exception = await Record.ExceptionAsync(() => query(context));

        Assert.IsType<SqlException>(exception);
    }

    [Fact]
    public Task Eager_loading_of_a_course() =>
        AssertTranslatesToSql(context => new CourseRepository(context).GetWithDetailsAsync(1));

    [Fact]
    public Task Slug_check_ignoring_soft_delete_filter() =>
        AssertTranslatesToSql(context => new CourseRepository(context).ExistsBySlugAsync("ef-core-in-practice"));

    [Fact]
    public Task Explicit_loading_of_a_category() =>
        AssertTranslatesToSql(context => new CategoryRepository(context).GetWithCoursesAsync(1));

    [Fact]
    public Task Category_lookups() =>
        AssertTranslatesToSql(async context =>
        {
            var categories = new CategoryRepository(context);
            await Record.ExceptionAsync(() => categories.GetByIdsAsync([1, 2]));
            await Record.ExceptionAsync(() => categories.ExistsByNameOrSlugAsync("Бази даних", "databases"));
            await categories.GetBySlugAsync("databases");
        });

    [Fact]
    public Task Recently_added_courses_through_the_join_entity() =>
        AssertTranslatesToSql(context =>
            new CourseCategoryRepository(context).GetRecentlyAddedAsync("programming", new DateTime(2026, 1, 1)));

    [Fact]
    public Task Group_by_on_the_join_entity() =>
        AssertTranslatesToSql(context => new CourseCategoryRepository(context).CountCoursesPerCategoryAsync());

    [Fact]
    public Task Instructor_email_check() =>
        AssertTranslatesToSql(context => new InstructorRepository(context).ExistsByEmailAsync("a@example.com"));

    [Theory]
    [InlineData("title", "asc")]
    [InlineData("price", "desc")]
    [InlineData("publishedAt", "asc")]
    [InlineData("rating", "desc")]
    [InlineData("createdAt", "asc")]
    public Task Catalog_specification_with_every_sort_field(string sortBy, string sortDir) =>
        AssertTranslatesToSql(context =>
        {
            var query = new CourseQuery
            {
                Search = "Core", Level = "Intermediate", CategoryId = 1, InstructorId = 2,
                MinPrice = 100, MaxPrice = 2000, IsPublished = true, SortBy = sortBy, SortDir = sortDir
            };
            return new CourseRepository(context).ListAsync(new CoursesFilterSpec(query, page: 2, pageSize: 5));
        });

    [Fact]
    public Task Catalog_count_uses_only_the_criteria() =>
        AssertTranslatesToSql(context =>
            new CourseRepository(context).CountAsync(new CoursesFilterSpec(new CourseQuery { CategoryId = 1 }, 1, 20)));

    [Fact]
    public Task Second_specification() =>
        AssertTranslatesToSql(context =>
            new CourseRepository(context).ListAsync(new InstructorCoursesSpec(instructorId: 2, publishedOnly: true)));
}
