using Ardalis.Specification;
using Courses.Bll.Dtos;
using Courses.Bll.Specifications;
using Courses.Domain.Entities;

namespace Courses.Tests;

/// <summary>
/// Специфікації не залежать від EF Core, тому їх критерії, сортування й сторінку можна
/// перевірити на звичайній колекції в пам'яті.
/// </summary>
public class SpecificationTests
{
    private static readonly Instructor Melnyk = TestData.Instructor(1, "melnyk@example.com");
    private static readonly Instructor Shevchenko = TestData.Instructor(2, "shevchenko@example.com");

    private static readonly List<Course> Catalog =
    [
        TestData.Course("C# для початківців", 1200m, CourseLevel.Beginner, published: true, Melnyk, id: 1),
        TestData.Course("ASP.NET Core Web API", 1800m, CourseLevel.Intermediate, published: true, Melnyk, id: 2),
        TestData.Course("Entity Framework Core", 1500m, CourseLevel.Intermediate, published: true, Shevchenko, id: 3),
        TestData.Course("MongoDB для .NET", 1400m, CourseLevel.Intermediate, published: true, Shevchenko, id: 4),
        TestData.Course("Мікросервіси на .NET Aspire", 2500m, CourseLevel.Advanced, published: true, id: 5),
        TestData.Course("Docker і Kubernetes", 2000m, CourseLevel.Advanced, published: false, id: 6)
    ];

    private static List<Course> Run(ISpecification<Course> specification) => specification.Evaluate(Catalog).ToList();

    [Fact]
    public void Filters_by_level_and_price_range()
    {
        var query = new CourseQuery { Level = "intermediate", MinPrice = 1450m, MaxPrice = 2000m };

        var result = Run(new CoursesFilterSpec(query, page: 1, pageSize: 20));

        Assert.Equal(["ASP.NET Core Web API", "Entity Framework Core"], result.Select(course => course.Title));
    }

    [Fact]
    public void Sorts_by_price_descending()
    {
        var query = new CourseQuery { SortBy = "price", SortDir = "desc" };

        var result = Run(new CoursesFilterSpec(query, page: 1, pageSize: 20));

        Assert.Equal([2500m, 2000m, 1800m, 1500m, 1400m, 1200m], result.Select(course => course.Price));
    }

    [Fact]
    public void Returns_the_requested_page()
    {
        var query = new CourseQuery { SortBy = "price" };

        var secondPage = Run(new CoursesFilterSpec(query, page: 2, pageSize: 2));

        Assert.Equal([1500m, 1800m], secondPage.Select(course => course.Price));
    }

    [Fact]
    public void Search_and_published_filter_combine()
    {
        var query = new CourseQuery { Search = "Docker", IsPublished = true };

        Assert.Empty(Run(new CoursesFilterSpec(query, page: 1, pageSize: 20)));
    }

    [Fact]
    public void Second_specification_selects_published_courses_of_one_instructor()
    {
        var result = Run(new InstructorCoursesSpec(instructorId: 2, publishedOnly: true));

        Assert.Equal(2, result.Count);
        Assert.All(result, course => Assert.Equal(2, course.InstructorId));
    }
}
