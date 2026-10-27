using Courses.Dal.ReadModels;
using Courses.Domain.Entities;

namespace Courses.Dal.Interfaces;

/// <summary>LINQ to Entities: вибірки «багато до багатьох» через проміжну сутність CourseCategory.</summary>
public interface ICourseCategoryRepository : IGenericRepository<CourseCategory>
{
    /// <summary>Курси, додані в категорію не раніше за <paramref name="since"/> — за власним полем AddedAt.</summary>
    Task<IReadOnlyList<CategoryCourseEntry>> GetRecentlyAddedAsync(
        string categorySlug, DateTime since, CancellationToken ct = default);

    /// <summary>Скільки курсів у кожній категорії і для скількох із них вона основна (IsPrimary).</summary>
    Task<IReadOnlyList<CategoryCourseCount>> CountCoursesPerCategoryAsync(CancellationToken ct = default);
}
