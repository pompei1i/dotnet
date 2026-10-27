using Courses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Courses.Dal.Seeding;

/// <summary>
/// Програмний сідінг — виконується в Program.cs ПІСЛЯ MigrateAsync. Довідник категорій до цього
/// моменту вже в базі: його вставила міграція (HasData у CategoryConfiguration), тож спершу
/// довідники, потім залежні записи — цілісність FK не порушується.
///
/// Ідемпотентний: перед вставкою кожного викладача й курсу перевіряється, чи він уже є
/// (за Email і Slug), тому повторний запуск нічого не дублює.
/// Дані ті самі, що в db/p2/seed.sql з Лаб. 01.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(CoursesDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var instructorsInserted = 0;
        var instructors = new Dictionary<string, Instructor>();
        foreach (var seed in InstructorSeeds)
        {
            var instructor = await db.Instructors.FirstOrDefaultAsync(i => i.Email == seed.Email, ct);
            if (instructor is null)
            {
                instructor = new Instructor(seed.Email, seed.FullName, seed.Bio);
                db.Instructors.Add(instructor);
                await db.SaveChangesAsync(ct);
                instructorsInserted++;
            }
            instructors[seed.Email] = instructor;
        }

        var categories = await db.Categories.ToDictionaryAsync(category => category.Slug, ct);

        var coursesInserted = 0;
        foreach (var seed in CourseSeeds)
        {
            // IgnoreQueryFilters: м'яко видалений курс теж займає свій Slug
            if (await db.Courses.IgnoreQueryFilters().AnyAsync(course => course.Slug == seed.Slug, ct))
                continue;

            var course = new Course(instructors[seed.InstructorEmail], seed.Title, seed.Slug, seed.Price, seed.Level);
            course.SetDetail(seed.Description, seed.Requirements, seed.DurationHours, seed.HasCertificate);

            foreach (var lesson in seed.Lessons)
                course.AddLesson(lesson.Title, lesson.DurationMinutes, lesson.IsFreePreview);

            foreach (var link in seed.Categories)
                course.AssignCategory(categories[link.CategorySlug], link.IsPrimary, link.AddedAt);

            if (seed.PublishedAt is { } publishedAt)
                course.Publish(publishedAt);

            if (seed.ReviewsCount > 0)
                course.UpdateRating(seed.AverageRating, seed.ReviewsCount);

            db.Courses.Add(course);

            // По одному курсу за раз: на чистій базі Id отримують значення 1–6 у цьому порядку —
            // ті самі Id курсів, що в копії довідника EnrollmentsDb і в feedback_db
            await db.SaveChangesAsync(ct);
            coursesInserted++;
        }

        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Database seeding finished: {InstructorsInserted} instructor(s) and {CoursesInserted} course(s) inserted",
            instructorsInserted, coursesInserted);
    }

    private sealed record InstructorSeed(string Email, string FullName, string Bio);

    private sealed record LessonSeed(string Title, int DurationMinutes, bool IsFreePreview = false);

    private sealed record CategoryLinkSeed(string CategorySlug, bool IsPrimary, DateTime AddedAt);

    private sealed record CourseSeed(
        string InstructorEmail,
        string Title,
        string Slug,
        decimal Price,
        CourseLevel Level,
        DateTime? PublishedAt,
        decimal? AverageRating,
        int ReviewsCount,
        string Description,
        string? Requirements,
        decimal DurationHours,
        bool HasCertificate,
        LessonSeed[] Lessons,
        CategoryLinkSeed[] Categories);

    private static DateTime At(int month, int day, int hour = 9, int minute = 0)
        => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static readonly InstructorSeed[] InstructorSeeds =
    [
        new("andrii.melnyk@example.com", "Андрій Мельник", ".NET-розробник із 10-річним досвідом, автор курсів з C# та ASP.NET Core"),
        new("iryna.shevchenko@example.com", "Ірина Шевченко", "Архітекторка баз даних, спеціалізується на SQL Server і MongoDB"),
        new("taras.bondarenko@example.com", "Тарас Бондаренко", "DevOps-інженер, будує мікросервісні платформи на .NET")
    ];

    private static readonly CourseSeed[] CourseSeeds =
    [
        new("andrii.melnyk@example.com", "C# для початківців", "csharp-for-beginners", 1200m, CourseLevel.Beginner,
            At(8, 1), 4.50m, 2,
            "Основи мови C#: типи, колекції, ООП, LINQ. Після курсу ви напишете консольний застосунок.", null, 24.0m, true,
            [new("Знайомство з .NET і першою програмою", 25, true), new("Типи даних і змінні", 40), new("Класи та об'єкти", 55)],
            [new("programming", true, At(8, 1))]),

        new("andrii.melnyk@example.com", "ASP.NET Core Web API", "aspnet-core-web-api", 1800m, CourseLevel.Intermediate,
            At(8, 10), 5.00m, 1,
            "Побудова REST API на ASP.NET Core: контролери, DI, валідація, Swagger, автентифікація.",
            "Знання C# на рівні курсу для початківців", 30.5m, true,
            [new("Мінімальний API та контролери", 35, true), new("Впровадження залежностей", 45), new("Валідація та обробка помилок", 50)],
            [new("web-development", true, At(8, 10)), new("programming", false, At(8, 12, 11, 30))]),

        new("iryna.shevchenko@example.com", "Entity Framework Core на практиці", "ef-core-in-practice", 1500m, CourseLevel.Intermediate,
            At(8, 15), 4.00m, 1,
            "Code First, Fluent API, міграції, оптимізація запитів та відстеження змін у EF Core.", "C# та основи SQL", 18.0m, true,
            [new("DbContext і перша міграція", 40, true), new("Зв'язки та Fluent API", 60)],
            [new("databases", true, At(8, 15)), new("programming", false, At(8, 16, 10))]),

        new("iryna.shevchenko@example.com", "MongoDB для .NET-розробників", "mongodb-for-dotnet", 1400m, CourseLevel.Intermediate,
            At(8, 20), null, 0,
            "Документна модель, вбудовування й посилання, індекси та aggregation pipeline з драйвером .NET.",
            "C# та базове розуміння баз даних", 14.5m, false,
            [new("Документи, колекції та BSON", 30, true), new("Вбудовування чи посилання", 45)],
            [new("databases", true, At(8, 20))]),

        new("taras.bondarenko@example.com", "Мікросервіси на .NET Aspire", "microservices-dotnet-aspire", 2500m, CourseLevel.Advanced,
            At(9, 1), null, 0,
            "Проєктування мікросервісів, .NET Aspire, обмін подіями, спостережуваність і розгортання.",
            "Досвід з ASP.NET Core та Docker", 40.0m, true,
            [new("Межі сервісів та обмежені контексти", 50, true), new("Оркестрація з .NET Aspire", 65)],
            [new("software-architecture", true, At(9, 1)), new("devops", false, At(9, 2, 15))]),

        // Чернетка: без уроків і без публікації
        new("taras.bondarenko@example.com", "Docker і Kubernetes для .NET", "docker-kubernetes-dotnet", 2000m, CourseLevel.Advanced,
            null, null, 0,
            "Контейнеризація .NET-застосунків і розгортання в Kubernetes. Курс у розробці.", "Досвід з ASP.NET Core", 20.0m, false,
            [],
            [new("devops", true, At(9, 15))])
    ];
}
