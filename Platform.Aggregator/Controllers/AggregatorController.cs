using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

/// <summary>Агрегувальні ендпоінти: одна відповідь замість кількох запитів клієнта до різних сервісів.</summary>
[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public sealed class AggregatorController(OrderCardService orderCards, CourseOverviewService courseOverviews) : ControllerBase
{
    /// <summary>
    /// Повна картка замовлення: замовлення + актуальні дані курсів + рейтинг і останні відгуки.
    /// Якщо courses-api чи reviews-api недоступний — 200 з частковими даними та Warnings.
    /// </summary>
    [HttpGet("orders/{orderId:long}/full")]
    [ProducesResponseType<OrderCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderCardDto>> GetOrderCard(long orderId, CancellationToken ct)
    {
        var card = await orderCards.GetOrderCardAsync(orderId, ct);
        return card is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Order not found", detail: $"Order {orderId} not found")
            : Ok(card);
    }

    /// <summary>Огляд курсу: каталог + відгуки + продажі, три сервіси паралельно.</summary>
    [HttpGet("courses/{courseId:long}/overview")]
    [ProducesResponseType<CourseOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseOverviewDto>> GetCourseOverview(long courseId, CancellationToken ct)
    {
        var overview = await courseOverviews.GetOverviewAsync(courseId, ct);
        return overview is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Course not found", detail: $"Course {courseId} not found")
            : Ok(overview);
    }
}
