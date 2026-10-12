using System.ComponentModel.DataAnnotations;
using Enrollments.Bll.Dtos;
using Enrollments.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Enrollments.Api.Controllers;

/// <summary>
/// Курси тут лише читаються: це локальна копія довідника, власником якого є сервіс Courses.
/// </summary>
[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courses) : ControllerBase
{
    /// <summary>Курси, доступні для купівлі, з пошуком за назвою.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResultDto<CourseDto>>> GetAvailable(
        [FromQuery] string? search,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await courses.GetAvailableAsync(search, page, pageSize, ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDto>> GetById(long id, CancellationToken ct)
        => Ok(await courses.GetByIdAsync(id, ct));

    /// <summary>Скільки оплачених замовлень містять курс і на яку суму.</summary>
    [HttpGet("{id:long}/sales")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseSalesDto>> GetSales(long id, CancellationToken ct)
        => Ok(await courses.GetSalesAsync(id, ct));
}
