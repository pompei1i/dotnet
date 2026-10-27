using Courses.Bll.Dtos;
using Courses.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Courses.Api.Controllers;

[ApiController]
[Route("api/instructors")]
public class InstructorsController(IInstructorService instructors) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InstructorDto>>> GetAll(CancellationToken ct)
        => Ok(await instructors.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorDto>> GetById(long id, CancellationToken ct)
        => Ok(await instructors.GetByIdAsync(id, ct));

    /// <summary>Курси викладача (друга специфікація).</summary>
    [HttpGet("{id:long}/courses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CourseDto>>> GetCourses(
        long id, [FromQuery] bool publishedOnly, CancellationToken ct)
        => Ok(await instructors.GetCoursesAsync(id, publishedOnly, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InstructorDto>> Create(CreateInstructorDto input, CancellationToken ct)
    {
        var created = await instructors.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
