using Courses.Domain.Exceptions;

namespace Courses.Domain.Entities;

/// <summary>
/// Розширений опис курсу (1:1 з Course): ключ CourseId є водночас зовнішнім ключем.
/// Створюється й змінюється лише через Course.SetDetail.
/// </summary>
public class CourseDetail
{
    public const int RequirementsMaxLength = 2000;
    public const decimal MaxDurationHours = 9999.9m;

    private CourseDetail() { }   // для EF Core

    internal CourseDetail(Course course, string description, string? requirements, decimal durationHours, bool hasCertificate)
    {
        Course = course;
        Update(description, requirements, durationHours, hasCertificate);
    }

    public long CourseId { get; private set; }
    public Course Course { get; private set; } = null!;
    public string Description { get; private set; } = "";
    public string? Requirements { get; private set; }
    public decimal DurationHours { get; private set; }
    public bool HasCertificate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    internal void Update(string description, string? requirements, decimal durationHours, bool hasCertificate)
    {
        if (durationHours <= 0 || durationHours > MaxDurationHours)
            throw new DomainException($"Duration must be between 0.1 and {MaxDurationHours} hours");

        Description = Guard.Text(description, "Description", int.MaxValue, minLength: 10);
        Requirements = Guard.OptionalText(requirements, "Requirements", RequirementsMaxLength);
        DurationHours = durationHours;
        HasCertificate = hasCertificate;
    }
}
