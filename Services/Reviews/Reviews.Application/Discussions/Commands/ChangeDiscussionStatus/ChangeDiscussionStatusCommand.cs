using Reviews.Application.Common.Interfaces;
using Reviews.Application.Discussions.Dtos;

namespace Reviews.Application.Discussions.Commands.ChangeDiscussionStatus;

/// <summary>Зміна стану теми: "open" | "resolved" | "closed". ExpectedVersion — з If-Match.</summary>
public sealed record ChangeDiscussionStatusCommand(string Id, string Status, int? ExpectedVersion)
    : ICommand<DiscussionDto>;
