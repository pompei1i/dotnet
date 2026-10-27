using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common;
using Reviews.Application.Discussions.Dtos;
using Reviews.Domain.Entities;
using Reviews.Domain.Enums;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Discussions.Commands.CreateDiscussion;

public sealed class CreateDiscussionCommandHandler(
    IDiscussionRepository discussions,
    IMapper mapper,
    ILogger<CreateDiscussionCommandHandler> logger) : IRequestHandler<CreateDiscussionCommand, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(CreateDiscussionCommand command, CancellationToken ct)
    {
        var discussion = new Discussion(
            command.CourseId,
            command.CourseTitle,
            command.Title,
            EnumText.Parse<DiscussionType>(command.Type),
            new Participant(command.AuthorId, EnumText.Parse<ParticipantRole>(command.AuthorRole), command.AuthorName),
            command.Tags,
            command.SourceReviewId is null ? null : ObjectId.Parse(command.SourceReviewId));

        await discussions.AddAsync(discussion, ct);

        logger.LogInformation("Discussion {DiscussionId} ({DiscussionType}) created for course {CourseId}",
            discussion.Id, command.Type, discussion.CourseId);
        return mapper.Map<DiscussionDto>(discussion);
    }
}
