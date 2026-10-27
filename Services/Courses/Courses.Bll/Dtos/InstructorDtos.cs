using System.ComponentModel.DataAnnotations;

namespace Courses.Bll.Dtos;

public record InstructorDto(long Id, string Email, string FullName, string? Bio);

public class CreateInstructorDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [StringLength(2000)]
    public string? Bio { get; set; }
}
