using Reviews.Domain.Common;
using Reviews.Domain.Enums;

namespace Reviews.Domain.ValueObjects;

/// <summary>
/// Учасник розмови (автор коментаря чи теми) — вбудований документ { userId, role, displayName }.
/// userId — Id студента в EnrollmentsDb або викладача в CoursesDb, залежно від ролі.
/// </summary>
public sealed class Participant : ValueObject
{
    public long UserId { get; }

    public ParticipantRole Role { get; }

    public string DisplayName { get; }

    public Participant(long userId, ParticipantRole role, string displayName)
    {
        UserId = Guard.Id(userId, "UserId");
        Role = role;
        DisplayName = Guard.Text(displayName, "DisplayName", Author.MaxNameLength);
    }

    public bool IsInstructor => Role == ParticipantRole.Instructor;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return Role;
        yield return DisplayName;
    }
}
