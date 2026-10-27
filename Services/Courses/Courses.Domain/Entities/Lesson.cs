using Courses.Domain.Exceptions;

namespace Courses.Domain.Entities;

/// <summary>Урок курсу (1:N з Course). Створюється лише через Course.AddLesson.</summary>
public class Lesson
{
    public const int TitleMaxLength = 200;
    public const int MaxDurationMinutes = 600;

    private Lesson() { }   // для EF Core

    internal Lesson(Course course, int orderIndex, string title, int durationMinutes, bool isFreePreview)
    {
        if (durationMinutes <= 0 || durationMinutes > MaxDurationMinutes)
            throw new DomainException($"Lesson duration must be between 1 and {MaxDurationMinutes} minutes");

        Course = course;
        OrderIndex = orderIndex;
        Title = Guard.Text(title, "Lesson title", TitleMaxLength);
        DurationMinutes = durationMinutes;
        IsFreePreview = isFreePreview;
    }

    public long Id { get; private set; }
    public long CourseId { get; private set; }
    public Course Course { get; private set; } = null!;
    public string Title { get; private set; } = "";
    public int OrderIndex { get; private set; }
    public int DurationMinutes { get; private set; }
    public bool IsFreePreview { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
}
