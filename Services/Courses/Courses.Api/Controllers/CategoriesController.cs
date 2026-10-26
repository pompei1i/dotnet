using Courses.Bll.Dtos;
using Courses.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Courses.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(ICategoryService categories) : ControllerBase
{
    /// <summary>Усі категорії з кількістю курсів у кожній.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await categories.GetAllAsync(ct));

    /// <summary>Категорія з її курсами (Explicit Loading: два SQL-запити).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryWithCoursesDto>> GetById(int id, CancellationToken ct)
        => Ok(await categories.GetWithCoursesAsync(id, ct));

    /// <summary>Курси, додані в категорію після дати (за замовчуванням — за 90 днів).</summary>
    [HttpGet("{slug}/recent-courses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RecentCourseDto>>> GetRecentCourses(
        string slug, [FromQuery] DateTime? since, CancellationToken ct)
        => Ok(await categories.GetRecentlyAddedAsync(slug, since, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(
        CreateCategoryDto input,
        [FromServices] IValidator<CreateCategoryDto> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var created = await categories.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
