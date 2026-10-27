using Courses.Bll.Dtos;
using Courses.Bll.Validation;

namespace Courses.Tests;

public class ValidatorTests
{
    private static CreateCourseDto ValidCourse() => new()
    {
        InstructorId = 1,
        Title = "Entity Framework Core на практиці",
        Slug = "ef-core-in-practice",
        Price = 1500m,
        Level = "Intermediate",
        Description = "Code First, Fluent API, міграції",
        DurationHours = 18m,
        CategoryIds = [3, 1],
        PrimaryCategoryId = 3
    };

    [Fact]
    public void Valid_course_passes()
        => Assert.True(new CreateCourseDtoValidator().Validate(ValidCourse()).IsValid);

    [Theory]
    [InlineData("EF Core")]
    [InlineData("ef--core")]
    [InlineData("-ef-core")]
    public void Slug_with_wrong_format_is_rejected(string slug)
    {
        var input = ValidCourse();
        input.Slug = slug;

        var result = new CreateCourseDtoValidator().Validate(input);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateCourseDto.Slug));
    }

    [Fact]
    public void Unknown_level_repeated_categories_and_foreign_primary_are_rejected()
    {
        var input = ValidCourse();
        input.Level = "Expert";
        input.CategoryIds = [1, 1];
        input.PrimaryCategoryId = 5;
        input.Price = 10.555m;

        var errors = new CreateCourseDtoValidator().Validate(input).Errors.Select(error => error.PropertyName).ToList();

        Assert.Contains(nameof(CreateCourseDto.Level), errors);
        Assert.Contains(nameof(CreateCourseDto.CategoryIds), errors);
        Assert.Contains(nameof(CreateCourseDto.PrimaryCategoryId), errors);
        Assert.Contains(nameof(CreateCourseDto.Price), errors);
    }

    [Theory]
    [InlineData("title", "asc", true)]
    [InlineData("price", "desc", true)]
    [InlineData("publishedAt", "DESC", true)]
    [InlineData("password", "asc", false)]
    [InlineData("title", "sideways", false)]
    public void Query_allows_only_known_sort_fields_and_directions(string sortBy, string sortDir, bool valid)
    {
        var query = new CourseQuery { SortBy = sortBy, SortDir = sortDir };

        Assert.Equal(valid, new CourseQueryValidator().Validate(query).IsValid);
    }

    [Fact]
    public void Query_rejects_inverted_price_range()
    {
        var query = new CourseQuery { MinPrice = 2000m, MaxPrice = 1000m };

        var result = new CourseQueryValidator().Validate(query);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CourseQuery.MaxPrice));
    }
}
