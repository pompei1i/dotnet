using Reviews.Application.Common.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

/// <summary>М'яке видалення відгуку (isDeleted = true); ExpectedVersion — з If-Match.</summary>
public sealed record DeleteReviewCommand(string Id, int? ExpectedVersion) : ICommand;
