using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common;
using Reviews.Application.Common.Exceptions;
using Reviews.Application.Discussions.Dtos;
using Reviews.Domain.Enums;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Discussions.Commands.ChangeDiscussionStatus;

public sealed class ChangeDiscussionStatusCommandHandler(
    IDiscussionRepository discussions,
    IMapper mapper,
    ILogger<ChangeDiscussionStatusCommandHandler> logger)
    : IRequestHandler<ChangeDiscussionStatusCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(ChangeDiscussionStatusCommand command, CancellationToken ct)
    {
        var discussion = await discussions.GetByIdAsync(ObjectId.Parse(command.Id), ct)
            ?? throw new NotFoundException("Discussion", command.Id);

        Concurrency.EnsureVersion(discussion.Version, command.ExpectedVersion, "Discussion", command.Id);

        var previous = discussion.Status;
        discussion.ChangeStatus(EnumText.Parse<DiscussionStatus>(command.Status));
        await discussions.UpdateAsync(discussion, ct);

        logger.LogInformation("Discussion {DiscussionId} moved from {PreviousStatus} to {Status}",
            discussion.Id, previous, discussion.Status);
        return mapper.Map<DiscussionDto>(discussion);
    }
}
