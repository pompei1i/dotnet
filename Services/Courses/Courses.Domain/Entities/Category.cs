namespace Courses.Domain.Entities;

/// <summary>Категорія каталогу — довідник. Таблиця dbo.Categories.</summary>
public class Category
{
    public const int NameMaxLength = 100;
    public const int SlugMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly List<CourseCategory> _courseCategories = [];

    private Category() { }   // для EF Core

    public Category(string name, string slug, string? description = null)
    {
        Rename(name);
        Slug = Guard.Slug(slug, SlugMaxLength);
        ChangeDescription(description);
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>Зв'язки з курсами через проміжну сутність (M:N).</summary>
    public IReadOnlyCollection<CourseCategory> CourseCategories => _courseCategories;

    public void Rename(string name) => Name = Guard.Text(name, "Category name", NameMaxLength, minLength: 2);

    public void ChangeDescription(string? description)
        => Description = Guard.OptionalText(description, "Description", DescriptionMaxLength);
}
