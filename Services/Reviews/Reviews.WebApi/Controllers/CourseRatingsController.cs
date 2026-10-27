using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Reviews.Dtos;
using Reviews.Application.Reviews.Queries.GetCourseRating;

namespace Reviews.WebApi.Controllers;

/// <summary>Рейтинг курсу, обчислений за активними відгуками.</summary>
[Route("api/courses/{courseId:long}/rating")]
public sealed class CourseRatingsController : ApiControllerBase
{
    /// <summary>Кількість відгуків, середня оцінка й розподіл оцінок 1–5.</summary>
    [HttpGet]
    [ProducesResponseType<CourseRatingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseRatingDto>> Get(long courseId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetCourseRatingQuery(courseId), ct));
}
