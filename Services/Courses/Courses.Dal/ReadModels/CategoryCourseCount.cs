namespace Courses.Dal.ReadModels;

/// <summary>Кількість курсів у категорії; PrimaryCount — для скількох із них категорія основна.</summary>
public record CategoryCourseCount(int CategoryId, string Name, string Slug, int CoursesCount, int PrimaryCount);
