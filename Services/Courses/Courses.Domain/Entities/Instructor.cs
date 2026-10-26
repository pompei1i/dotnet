namespace Courses.Domain.Entities;

/// <summary>Викладач. Таблиця dbo.Instructors.</summary>
public class Instructor : IAuditable
{
    public const int EmailMaxLength = 256;
    public const int FullNameMaxLength = 200;
    public const int BioMaxLength = 2000;

    private readonly List<Course> _courses = [];

    private Instructor() { }   // для EF Core

    public Instructor(string email, string fullName, string? bio = null)
    {
        Email = Guard.Email(email, EmailMaxLength);
        Rename(fullName);
        ChangeBio(bio);
    }

    public long Id { get; private set; }
    public string Email { get; private set; } = "";
    public string FullName { get; private set; } = "";
    public string? Bio { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Курси викладача (1:N).</summary>
    public IReadOnlyCollection<Course> Courses => _courses;

    public void Rename(string fullName) => FullName = Guard.Text(fullName, "Full name", FullNameMaxLength, minLength: 2);

    public void ChangeBio(string? bio) => Bio = Guard.OptionalText(bio, "Bio", BioMaxLength);
}
