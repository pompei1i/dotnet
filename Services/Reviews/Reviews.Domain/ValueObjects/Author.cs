using Reviews.Domain.Common;

namespace Reviews.Domain.ValueObjects;

/// <summary>
/// Автор відгуку — вбудований документ author { studentId, displayName }.
/// studentId посилається на студента в EnrollmentsDb (без FK), displayName — денормалізована копія імені.
/// </summary>
public sealed class Author : ValueObject
{
    public const int MaxNameLength = 200;

    public long StudentId { get; }

    public string DisplayName { get; }

    public Author(long studentId, string displayName)
    {
        StudentId = Guard.Id(studentId, "StudentId");
        DisplayName = Guard.Text(displayName, "DisplayName", MaxNameLength);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StudentId;
        yield return DisplayName;
    }
}
