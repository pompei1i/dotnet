using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Commands.AddComment;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Dtos;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.WebApi.Contracts;
using Reviews.WebApi.Http;

namespace Reviews.WebApi.Controllers;

/// <summary>Відгуки про курси (колекція reviews).</summary>
[Route("api/reviews")]
public sealed class ReviewsController : ApiControllerBase
{
    /// <summary>Список відгуків з фільтрами, сортуванням і пагінацією (виконуються в MongoDB).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ReviewListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewListItemDto>>> GetList(
        [FromQuery] ReviewListRequest request, CancellationToken ct)
    {
        var page = await Mediator.Send(Mapper.Map<GetReviewsListQuery>(request), ct);
        Response.AddPagination(page);
        return Ok(page);
    }

    /// <summary>Відгук за Id разом із коментарями; версія документа — у заголовку ETag.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
    {
        var review = await Mediator.Send(new GetReviewByIdQuery(id), ct);
        Response.SetVersionETag(review.Version);
        return Ok(review);
    }

    /// <summary>Новий відгук; Location містить рядковий ObjectId.</summary>
    [HttpPost]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Create(CreateReviewRequest request, CancellationToken ct)
    {
        var review = await Mediator.Send(Mapper.Map<CreateReviewCommand>(request), ct);
        Response.SetVersionETag(review.Version);
        return CreatedAtAction(nameof(GetById), new { id = review.Id }, review);
    }

    /// <summary>
    /// Зміна відгуку. If-Match: "версія" з ETag — якщо документ тим часом змінили, відповідь 409.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Update(
        string id,
        UpdateReviewRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = Mapper.Map<UpdateReviewCommand>(request) with
        {
            Id = id,
            ExpectedVersion = ResponseHeaders.ParseIfMatchVersion(ifMatch)
        };
        var review = await Mediator.Send(command, ct);
        Response.SetVersionETag(review.Version);
        return Ok(review);
    }

    /// <summary>М'яке видалення відгуку; If-Match — як у PUT.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        string id, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken ct)
    {
        await Mediator.Send(new DeleteReviewCommand(id, ResponseHeaders.ParseIfMatchVersion(ifMatch)), ct);
        return NoContent();
    }

    /// <summary>Коментар до відгуку — додається у вбудований масив comments.</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> AddComment(string id, AddCommentRequest request, CancellationToken ct)
    {
        var review = await Mediator.Send(Mapper.Map<AddCommentCommand>(request) with { ReviewId = id }, ct);
        Response.SetVersionETag(review.Version);
        return CreatedAtAction(nameof(GetById), new { id = review.Id }, review);
    }
}
