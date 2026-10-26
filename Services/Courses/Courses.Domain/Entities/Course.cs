using Courses.Domain.Exceptions;

namespace Courses.Domain.Entities;

/// <summary>
/// Корінь агрегату «Курс» (таблиця dbo.Courses) — аналог CatalogItem з eShop.
/// Стан змінюється лише методами, які перевіряють правила самої сутності:
/// ціна не від'ємна, опублікувати можна лише курс з описом і уроками, у курсу одна
/// основна категорія. Правила, яким потрібні інші дані з бази (унікальність Slug),
/// перевіряє сервіс BLL.
/// </summary>
public class Course : IAuditable
{
    public const int TitleMaxLength = 200;
    public const int SlugMaxLength = 200;
    public const int LanguageMaxLength = 10;
    public const decimal MaxPrice = 1_000_000m;

    private readonly List<Lesson> _lessons = [];
    private readonly List<CourseCategory> _courseCategories = [];

    private Course() { }   // для EF Core

    public Course(Instructor instructor, string title, string slug, decimal price, CourseLevel level, string language = "uk")
    {
        Instructor = instructor ?? throw new DomainException("Course must have an instructor");
        InstructorId = instructor.Id;
        Rename(title);
        Slug = Guard.Slug(slug, SlugMaxLength);
        ChangePrice(price);
        Level = level;
        Language = Guard.Text(language, "Language", LanguageMaxLength, minLength: 2);
    }

    public long Id { get; private set; }
    public long InstructorId { get; private set; }
    public Instructor Instructor { get; private set; } = null!;
    public string Title { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public decimal Price { get; private set; }
    public CourseLevel Level { get; private set; }
    public string Language { get; private set; } = "uk";
    public bool IsPublished { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    /// <summary>Копія рейтингу з feedback_db (політика дублювання з Лаб. 01).</summary>
    public decimal? AverageRating { get; private set; }

    /// <summary>Копія кількості відгуків з feedback_db.</summary>
    public int ReviewsCount { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    /// <summary>Токен оптимістичної конкурентності (ROWVERSION).</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Розширений опис (1:1).</summary>
    public CourseDetail? Detail { get; private set; }

    /// <summary>Уроки (1:N).</summary>
    public IReadOnlyCollection<Lesson> Lessons => _lessons;

    /// <summary>Категорії через проміжну сутність (M:N).</summary>
    public IReadOnlyCollection<CourseCategory> CourseCategories => _courseCategories;

    public void Rename(string title) => Title = Guard.Text(title, "Title", TitleMaxLength, minLength: 3);

    public void ChangePrice(decimal newPrice)
    {
        if (newPrice < 0)
            throw new DomainException("Price cannot be negative");
        if (newPrice > MaxPrice)
            throw new DomainException($"Price cannot exceed {MaxPrice}");

        Price = newPrice;
    }

    public void ChangeLevel(CourseLevel level) => Level = level;

    /// <summary>Створює або оновлює розширений опис курсу.</summary>
    public void SetDetail(string description, string? requirements, decimal durationHours, bool hasCertificate)
    {
        if (Detail is null)
            Detail = new CourseDetail(this, description, requirements, durationHours, hasCertificate);
        else
            Detail.Update(description, requirements, durationHours, hasCertificate);
    }

    /// <summary>
    /// Додає урок у кінець курсу. Номер уроку рахується з уже завантажених уроків,
    /// тому курс має бути прочитаний разом з ними.
    /// </summary>
    public Lesson AddLesson(string title, int durationMinutes, bool isFreePreview = false)
    {
        var nextIndex = _lessons.Count == 0 ? 1 : _lessons.Max(lesson => lesson.OrderIndex) + 1;
        var lesson = new Lesson(this, nextIndex, title, durationMinutes, isFreePreview);
        _lessons.Add(lesson);
        return lesson;
    }

    /// <summary>
    /// Додає курс у категорію. Основна категорія в курсу лише одна: нова основна
    /// знімає позначку з попередньої, а перша додана категорія стає основною сама.
    /// Курс має бути прочитаний разом з категоріями.
    /// </summary>
    public void AssignCategory(Category category, bool isPrimary, DateTime addedAt)
    {
        if (_courseCategories.Any(link => link.CategoryId == category.Id))
            throw new DomainException($"Course is already in category '{category.Name}'");

        if (isPrimary)
            foreach (var link in _courseCategories)
                link.ClearPrimary();

        _courseCategories.Add(new CourseCategory(this, category, isPrimary || _courseCategories.Count == 0, addedAt));
    }

    public void MakeCategoryPrimary(int categoryId)
    {
        var target = _courseCategories.FirstOrDefault(link => link.CategoryId == categoryId)
                     ?? throw new DomainException($"Course is not in category {categoryId}");

        foreach (var link in _courseCategories)
            link.ClearPrimary();
        target.MarkPrimary();
    }

    public void Publish(DateTime publishedAt)
    {
        if (IsPublished)
            throw new DomainException("Course is already published");
        if (Detail is null)
            throw new DomainException("Course needs a description before it can be published");
        if (_lessons.Count == 0)
            throw new DomainException("Course needs at least one lesson before it can be published");

        IsPublished = true;
        PublishedAt = publishedAt;
    }

    public void Unpublish()
    {
        if (!IsPublished)
            throw new DomainException("Course is not published");

        IsPublished = false;
    }

    /// <summary>Оновлює копію рейтингу; викликатиметься обробником події з сервісу Feedback.</summary>
    public void UpdateRating(decimal? averageRating, int reviewsCount)
    {
        if (averageRating is < 1 or > 5)
            throw new DomainException("Average rating must be between 1 and 5");
        if (reviewsCount < 0)
            throw new DomainException("Reviews count cannot be negative");

        AverageRating = averageRating;
        ReviewsCount = reviewsCount;
    }

    /// <summary>М'яке видалення. Опублікований курс спершу треба зняти з публікації.</summary>
    public void Delete()
    {
        if (IsPublished)
            throw new DomainException("Unpublish the course before deleting it");

        IsDeleted = true;
    }
}
