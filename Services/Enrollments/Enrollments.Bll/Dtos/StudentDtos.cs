using System.ComponentModel.DataAnnotations;

namespace Enrollments.Bll.Dtos;

/// <summary>Студент у відповіді API. RowVersion повертається клієнту для подальшого оновлення.</summary>
public record StudentDto(
    long Id,
    string Email,
    string FullName,
    string? Phone,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    byte[] RowVersion);

public class CreateStudentDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Phone, StringLength(20)]
    public string? Phone { get; set; }
}

public class UpdateStudentDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    /// <summary>Значення RowVersion, отримане під час читання студента (base64 у JSON).</summary>
    [Required, MinLength(8), MaxLength(8)]
    public byte[] RowVersion { get; set; } = [];
}
