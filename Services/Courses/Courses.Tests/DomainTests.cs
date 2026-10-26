using Courses.Domain.Entities;
using Courses.Domain.Exceptions;

namespace Courses.Tests;

/// <summary>Правила, які перевіряє сама сутність, — без бази й без сервісів.</summary>
public class DomainTests
{
    [Fact]
    public void Constructor_rejects_negative_price()
        => Assert.Throws<DomainException>(() => TestData.Course(price: -1m));

    [Fact]
    public void Constructor_rejects_invalid_slug()
        => Assert.Throws<DomainException>(() =>
            new Course(TestData.Instructor(), "C# для початківців", "Not A Slug", 100m, CourseLevel.Beginner));

    [Fact]
    public void ChangePrice_rejects_negative_value_and_keeps_old_price()
    {
        var course = TestData.Course(price: 1200m);

        Assert.Throws<DomainException>(() => course.ChangePrice(-5m));
        Assert.Equal(1200m, course.Price);
    }

    [Fact]
    public void Publish_requires_description_and_lessons()
    {
        var course = TestData.Course();

        var noDetail = Assert.Throws<DomainException>(() => course.Publish(DateTime.UtcNow));
        Assert.Contains("description", noDetail.Message);

        course.SetDetail("Повний опис курсу з усіма темами", null, 12m, true);
        var noLessons = Assert.Throws<DomainException>(() => course.Publish(DateTime.UtcNow));
        Assert.Contains("lesson", noLessons.Message);

        course.AddLesson("Вступ", 15);
        course.Publish(DateTime.UtcNow);
        Assert.True(course.IsPublished);
        Assert.NotNull(course.PublishedAt);
    }

    [Fact]
    public void Publish_twice_is_rejected()
    {
        var course = TestData.Course(published: true);

        Assert.Throws<DomainException>(() => course.Publish(DateTime.UtcNow));
    }

    [Fact]
    public void AddLesson_numbers_lessons_one_after_another()
    {
        var course = TestData.Course();

        var first = course.AddLesson("Перший", 10);
        var second = course.AddLesson("Другий", 20, isFreePreview: true);

        Assert.Equal(1, first.OrderIndex);
        Assert.Equal(2, second.OrderIndex);
        Assert.True(second.IsFreePreview);
    }

    [Fact]
    public void First_category_becomes_primary_and_a_new_primary_replaces_it()
    {
        var course = TestData.Course();
        var programming = TestData.Category(1);
        var webDevelopment = TestData.Category(2, "Веб-розробка");

        course.AssignCategory(programming, isPrimary: false, DateTime.UtcNow);
        Assert.True(course.CourseCategories.Single().IsPrimary);

        course.AssignCategory(webDevelopment, isPrimary: true, DateTime.UtcNow);
        Assert.Equal(2, course.CourseCategories.Single(link => link.IsPrimary).CategoryId);
        Assert.Single(course.CourseCategories, link => link.IsPrimary);
    }

    [Fact]
    public void Same_category_cannot_be_assigned_twice()
    {
        var course = TestData.Course();
        var category = TestData.Category(1);
        course.AssignCategory(category, isPrimary: true, DateTime.UtcNow);

        Assert.Throws<DomainException>(() => course.AssignCategory(category, isPrimary: false, DateTime.UtcNow));
    }

    [Fact]
    public void Published_course_cannot_be_deleted()
    {
        var course = TestData.Course(published: true);

        Assert.Throws<DomainException>(course.Delete);

        course.Unpublish();
        course.Delete();
        Assert.True(course.IsDeleted);
    }

    [Fact]
    public void Rating_outside_one_to_five_is_rejected()
    {
        var course = TestData.Course();

        Assert.Throws<DomainException>(() => course.UpdateRating(5.5m, 3));
        course.UpdateRating(4.5m, 2);
        Assert.Equal(4.5m, course.AverageRating);
    }
}
