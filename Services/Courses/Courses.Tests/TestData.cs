using Courses.Domain.Entities;

namespace Courses.Tests;

/// <summary>Сутності для тестів. Id у сутностях приватні, тому тести виставляють їх рефлексією.</summary>
internal static class TestData
{
    public static T WithId<T>(this T entity, object id)
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
        return entity;
    }

    public static Instructor Instructor(long id = 1, string email = "andrii@example.com")
        => new Instructor(email, "Андрій Мельник").WithId(id);

    public static Category Category(int id, string name = "Програмування", string? slug = null)
        => new Category(name, slug ?? $"category-{id}").WithId(id);

    public static Course Course(
        string title = "C# для початківців",
        decimal price = 1200m,
        CourseLevel level = CourseLevel.Beginner,
        bool published = false,
        Instructor? instructor = null,
        long id = 1,
        string? slug = null)
    {
        var course = new Course(instructor ?? Instructor(), title, slug ?? $"course-{id}", price, level).WithId(id);
        if (published)
        {
            course.SetDetail("Опис курсу для перевірки публікації", null, 10m, hasCertificate: false);
            course.AddLesson("Вступ", 20);
            course.Publish(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        }
        return course;
    }
}
