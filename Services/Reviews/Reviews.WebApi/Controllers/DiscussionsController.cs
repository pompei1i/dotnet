using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Models;
using Reviews.Application.Discussions.Commands.ChangeDiscussionStatus;
using Reviews.Application.Discussions.Commands.CreateDiscussion;
using Reviews.Application.Discussions.Dtos;
using Reviews.Application.Discussions.Queries.GetDiscussionById;
using Reviews.Application.Discussions.Queries.GetDiscussionsList;
using Reviews.WebApi.Contracts;
using Reviews.WebApi.Http;

namespace Reviews.WebApi.Controllers;

/// <summary>Теми обговорення курсів (колекція discussions).</summary>
[Route("api/discussions")]
public sealed class DiscussionsController : ApiControllerBase
{
    /// <summary>Теми з фільтром за курсом, станом і тегом та пошуком по заголовку.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<DiscussionListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<DiscussionListItemDto>>> GetList(
        [FromQuery] DiscussionListRequest request, CancellationToken ct)
    {
        var page = await Mediator.Send(Mapper.Map<GetDiscussionsListQuery>(request), ct);
        Response.AddPagination(page);
        return Ok(page);
    }

    [HttpGet("{id}")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscussionDto>> GetById(string id, CancellationToken ct)
    {
        var discussion = await Mediator.Send(new GetDiscussionByIdQuery(id), ct);
        Response.SetVersionETag(discussion.Version);
        return Ok(discussion);
    }

    /// <summary>Нова тема: питання студента або оголошення викладача.</summary>
    [HttpPost]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiscussionDto>> Create(CreateDiscussionRequest request, CancellationToken ct)
    {
        var discussion = await Mediator.Send(Mapper.Map<CreateDiscussionCommand>(request), ct);
        Response.SetVersionETag(discussion.Version);
        return CreatedAtAction(nameof(GetById), new { id = discussion.Id }, discussion);
    }

    /// <summary>Зміна стану теми (open / resolved / closed); If-Match — очікувана версія.</summary>
    [HttpPut("{id}/status")]
    [ProducesResponseType<DiscussionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiscussionDto>> ChangeStatus(
        string id,
        ChangeDiscussionStatusRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = Mapper.Map<ChangeDiscussionStatusCommand>(request) with
        {
            Id = id,
            ExpectedVersion = ResponseHeaders.ParseIfMatchVersion(ifMatch)
        };
        var discussion = await Mediator.Send(command, ct);
        Response.SetVersionETag(discussion.Version);
        return Ok(discussion);
    }
}
