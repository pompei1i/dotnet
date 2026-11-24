using Courses.Api.Metrics;
using Courses.Bll.Dtos;
using Courses.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Courses.Api.Controllers;

/// <summary>
/// Тонкий контролер каталогу: валідація запиту, виклик сервісу, HTTP-статус.
/// До DbContext контролер не звертається; винятки перетворює DomainExceptionHandler.
/// Лаб. 07: тривалість кожної операції (успіх і відмова) — courses.operation.duration, успіх — бізнес-лічильники.
/// </summary>
[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courses, CatalogMetrics metrics) : ControllerBase
{
    /// <summary>Сторінка каталогу з фільтрами й сортуванням; метадані сторінки — у тілі та заголовках X-Total-*.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CourseDto>>> GetList(
        [FromQuery] CourseQuery query,
        [FromServices] IValidator<CourseQuery> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var page = await courses.GetListAsync(query, ct);
        Response.AddPaginationHeaders(page);
        return Ok(page);
    }

    /// <summary>Картка курсу з викладачем, описом, уроками й категоріями.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDetailsDto>> GetById(long id, CancellationToken ct)
        => Ok(await metrics.MeasureAsync("get", () => courses.GetByIdAsync(id, ct)));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourseDetailsDto>> Create(
        CreateCourseDto input,
        [FromServices] IValidator<CreateCourseDto> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var created = await metrics.MeasureAsync("create", () => courses.CreateAsync(input, ct));
        metrics.CourseCreated(created.Level);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePrice(
        long id,
        ChangePriceDto input,
        [FromServices] IValidator<ChangePriceDto> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        await metrics.MeasureAsync("change_price", () => courses.ChangePriceAsync(id, input, ct));
        metrics.PriceChanged();
        return NoContent();
    }

    /// <summary>Публікує курс; без опису або уроків — 409.</summary>
    [HttpPost("{id:long}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync("publish", () => courses.PublishAsync(id, ct));
        metrics.PublicationChanged(published: true);
        return NoContent();
    }

    [HttpPost("{id:long}/unpublish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unpublish(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync("unpublish", () => courses.UnpublishAsync(id, ct));
        metrics.PublicationChanged(published: false);
        return NoContent();
    }

    /// <summary>Додає урок у кінець курсу.</summary>
    [HttpPost("{id:long}/lessons")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonDto>> AddLesson(long id, CreateLessonDto input, CancellationToken ct)
    {
        var lesson = await metrics.MeasureAsync("add_lesson", () => courses.AddLessonAsync(id, input, ct));
        metrics.LessonAdded();
        return CreatedAtAction(nameof(GetById), new { id }, lesson);
    }

    /// <summary>Додає курс у категорію (M:N); повторне додавання — 409.</summary>
    [HttpPost("{id:long}/categories")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCategory(long id, AssignCategoryDto input, CancellationToken ct)
    {
        await metrics.MeasureAsync("assign_category", () => courses.AssignCategoryAsync(id, input, ct));
        return NoContent();
    }

    /// <summary>М'яко видаляє курс; опублікований курс спершу треба зняти з публікації (409).</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync("delete", () => courses.DeleteAsync(id, ct));
        metrics.CourseDeleted();
        return NoContent();
    }
}
