namespace Courses.Domain.Entities;

/// <summary>
/// Проміжна сутність M:N між Course і Category з ВЛАСНИМИ полями: AddedAt (коли курс
/// додано в категорію) та IsPrimary (основна категорія курсу). Автоматичне M:N в EF Core
/// не дало б зберегти ці поля. Створюється лише через Course.AssignCategory.
/// </summary>
public class CourseCategory
{
    private CourseCategory() { }   // для EF Core

    internal CourseCategory(Course course, Category category, bool isPrimary, DateTime addedAt)
    {
        Course = course;
        Category = category;
        CategoryId = category.Id;
        IsPrimary = isPrimary;
        AddedAt = addedAt;
    }

    public long CourseId { get; private set; }
    public Course Course { get; private set; } = null!;
    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public DateTime AddedAt { get; private set; }
    public bool IsPrimary { get; private set; }

    internal void MarkPrimary() => IsPrimary = true;

    internal void ClearPrimary() => IsPrimary = false;
}
