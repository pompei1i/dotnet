using System.ComponentModel.DataAnnotations;
using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

[ApiController]
[Route("api/students")]
public class StudentsController(IStudentService students, IOrderService orders) : ControllerBase
{
    /// <summary>Сторінка студентів із пошуком за початком імені або Email.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResultDto<StudentDto>>> GetPaged(
        [FromQuery] string? search,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await students.GetPagedAsync(search, page, pageSize, ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDto>> GetById(long id, CancellationToken ct)
        => Ok(await students.GetByIdAsync(id, ct));

    /// <summary>Замовлення студента з необов'язковим фільтром за статусом.</summary>
    [HttpGet("{id:long}/orders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResultDto<OrderSummaryDto>>> GetOrders(
        long id,
        [FromQuery] string? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await orders.GetByStudentAsync(id, status, page, pageSize, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentDto>> Create(CreateStudentDto input, CancellationToken ct)
    {
        var created = await students.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Оновлює студента; RowVersion із запиту захищає від втрати чужих змін.</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentDto>> Update(long id, UpdateStudentDto input, CancellationToken ct)
        => Ok(await students.UpdateAsync(id, input, ct));

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await students.DeleteAsync(id, ct);
        return NoContent();
    }
}
